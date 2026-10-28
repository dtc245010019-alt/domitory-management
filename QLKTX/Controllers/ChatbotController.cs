using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QLKTX.Models;

namespace QLKTX.Controllers
{
    public class ChatbotController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ChatbotController> _logger;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

        public ChatbotController(IWebHostEnvironment env, ApplicationDbContext context,
            IHttpClientFactory httpClientFactory, ILogger<ChatbotController> logger,
            Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _env = env;
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        [HttpPost]
        public async Task<IActionResult> Ask([FromBody] ChatRequest request)
        {
            try
            {
                return await AskInternal(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không xác định trong ChatbotController.Ask");
                return Json(new { response = "Xin lỗi, hệ thống đang gặp sự cố. Vui lòng thử lại sau." });
            }
        }

        // =========================================================================
        // API THỰC THI HÀNH ĐỘNG TRỰC TIẾP TỪ KHUNG CHAT (AI ACTION AGENT / FUNCTION CALLING)
        // =========================================================================
        [HttpPost]
        public async Task<IActionResult> ExecuteAction([FromBody] ChatActionExecuteRequest req)
        {
            try
            {
                if (req == null || string.IsNullOrWhiteSpace(req.ActionType))
                    return Json(new { success = false, message = "Yêu cầu thực thi không hợp lệ." });

                string sessionMaSV = HttpContext.Session.GetString("MaSV_DangNhap") ?? "";
                bool isAdmin = HttpContext.Session.GetString("Admin_DangNhap") == "true";

                // Xác định Sinh viên và Phòng thực hiện thao tác
                SinhVien? sv = null;
                if (!string.IsNullOrWhiteSpace(req.MaSV))
                {
                    sv = await _context.SinhViens.FirstOrDefaultAsync(s => s.MaSV == req.MaSV.Trim().ToUpper());
                }
                if (sv == null && !string.IsNullOrWhiteSpace(sessionMaSV))
                {
                    sv = await _context.SinhViens.FirstOrDefaultAsync(s => s.MaSV == sessionMaSV);
                }

                string maPhong = !string.IsNullOrWhiteSpace(req.MaPhong)
                    ? NormalizeRoomCode(req.MaPhong)
                    : (sv?.MaPhong ?? "");

                // Khớp mã phòng chuẩn trong CSDL
                if (!string.IsNullOrWhiteSpace(maPhong))
                {
                    var matchedRoom = await _context.Phongs.FirstOrDefaultAsync(p =>
                        p.MaPhong.ToUpper() == maPhong.ToUpper());
                    if (matchedRoom != null)
                        maPhong = matchedRoom.MaPhong;
                }

                // Nếu chưa có SV cụ thể nhưng có Mã phòng -> lấy đại diện SV đang ở phòng đó để đảm bảo khóa ngoại hợp lệ
                if (sv == null && !string.IsNullOrWhiteSpace(maPhong))
                {
                    sv = await _context.SinhViens.FirstOrDefaultAsync(s => s.MaPhong == maPhong && s.TinhTrangLuuTru == "Đang ở")
                         ?? await _context.SinhViens.FirstOrDefaultAsync(s => s.MaPhong == maPhong);
                }
                if (sv == null)
                {
                    sv = await _context.SinhViens.FirstOrDefaultAsync(s => s.TinhTrangLuuTru == "Đang ở")
                         ?? await _context.SinhViens.FirstOrDefaultAsync();
                }

                if (sv == null)
                    return Json(new { success = false, message = "Không tìm thấy hồ sơ sinh viên trong hệ thống để thực hiện thao tác." });

                if (string.IsNullOrWhiteSpace(maPhong))
                    maPhong = sv.MaPhong ?? (await _context.Phongs.Select(p => p.MaPhong).FirstOrDefaultAsync() ?? "101");

                // 1. TẠO BÁO CÁO SỰ CỐ / BÁO HỎNG THIẾT BỊ
                if (req.ActionType == "TAO_BAO_CAO_SU_CO")
                {
                    string tenSuCo = string.IsNullOrWhiteSpace(req.TenSuCo) ? "Báo hỏng cơ sở vật chất" : req.TenSuCo.Trim();
                    string mucDo = string.IsNullOrWhiteSpace(req.MucDoKhanCap) ? "Bình thường" : req.MucDoKhanCap.Trim();
                    string moTa = string.IsNullOrWhiteSpace(req.MoTa)
                        ? $"Báo cáo sự cố '{tenSuCo}' tại phòng P.{maPhong} (Thực hiện qua Trợ lý AI)"
                        : req.MoTa.Trim();

                    string maSC = "SC_" + DateTime.Now.ToString("yyyyMMddHHmmss");
                    var bc = new BaoCaoSuCo
                    {
                        MaSuCo = maSC,
                        TenSuCo = tenSuCo,
                        MoTa = moTa,
                        MucDoKhanCap = mucDo,
                        HinhAnh = "",
                        MaSV = sv.MaSV,
                        MaPhong = maPhong,
                        TrangThai = "Chờ xử lý",
                        NgayBaoCao = DateTime.Now
                    };

                    _context.BaoCaoSuCos.Add(bc);
                    await _context.SaveChangesAsync();

                    return Json(new
                    {
                        success = true,
                        message = $"✅ Đã gửi báo cáo sự cố thành công! Mã phiếu: {maSC}",
                        receipt = new
                        {
                            maPhieu = maSC,
                            loaiPhieu = "Báo cáo sự cố kỹ thuật",
                            maPhong = "P." + maPhong,
                            nguoiGui = $"{sv.MaSV} - {sv.HoTen}",
                            noiDung = tenSuCo,
                            chiTiet = $"Mức độ: {mucDo} | Mô tả: {moTa}",
                            trangThai = "Chờ xử lý",
                            thoiGian = bc.NgayBaoCao.ToString("dd/MM/yyyy HH:mm")
                        }
                    });
                }

                // 2. GỬI YÊU CẦU PHÒNG (XIN CHUYỂN PHÒNG / ĐĂNG KÝ GIA HẠN Ở / ĐĂNG KÝ TRẢ PHÒNG)
                if (req.ActionType == "GUI_YEU_CAU_PHONG")
                {
                    string loaiYeuCau = string.IsNullOrWhiteSpace(req.LoaiYeuCau) ? "Xin chuyển phòng" : req.LoaiYeuCau.Trim();
                    string lyDo = string.IsNullOrWhiteSpace(req.LyDo)
                        ? $"Sinh viên gửi yêu cầu '{loaiYeuCau}' tự động qua Trợ lý AI"
                        : req.LyDo.Trim();

                    string phongMongMuon;
                    if (loaiYeuCau.Contains("chuyển phòng", StringComparison.OrdinalIgnoreCase))
                    {
                        string phongDen = NormalizeRoomCode(req.PhongChuyenDen ?? "");
                        if (string.IsNullOrWhiteSpace(phongDen))
                            return Json(new { success = false, message = "Vui lòng chọn hoặc nhập mã phòng muốn chuyển đến!" });

                        if (phongDen.Equals(maPhong, StringComparison.OrdinalIgnoreCase))
                            return Json(new { success = false, message = "Mã phòng muốn chuyển đến phải khác phòng hiện tại!" });

                        phongMongMuon = $"Từ phòng {maPhong} -> Chuyển sang phòng {phongDen}";
                    }
                    else
                    {
                        phongMongMuon = $"Phòng hiện tại: {maPhong}";
                    }

                    string maYC = "YC_" + DateTime.Now.ToString("yyyyMMddHHmmss");
                    var yeuCau = new BaoCaoSuCo
                    {
                        MaSuCo = maYC,
                        TenSuCo = $"[{loaiYeuCau}] Nguyện vọng: {phongMongMuon}",
                        MoTa = lyDo,
                        MucDoKhanCap = "Bình thường",
                        HinhAnh = "",
                        MaSV = sv.MaSV,
                        MaPhong = maPhong,
                        TrangThai = "Chờ xử lý",
                        NgayBaoCao = DateTime.Now
                    };

                    _context.BaoCaoSuCos.Add(yeuCau);
                    await _context.SaveChangesAsync();

                    return Json(new
                    {
                        success = true,
                        message = $"✅ Đã gửi đơn '{loaiYeuCau}' thành công! Mã đơn: {maYC}",
                        receipt = new
                        {
                            maPhieu = maYC,
                            loaiPhieu = loaiYeuCau,
                            maPhong = "P." + maPhong,
                            nguoiGui = $"{sv.MaSV} - {sv.HoTen}",
                            noiDung = phongMongMuon,
                            chiTiet = $"Lý do: {lyDo}",
                            trangThai = "Chờ Ban Quản lý duyệt",
                            thoiGian = yeuCau.NgayBaoCao.ToString("dd/MM/yyyy HH:mm")
                        }
                    });
                }

                // 3. THANH TOÁN HÓA ĐƠN TRỰC TIẾP QUA CHAT
                if (req.ActionType == "THANH_TOAN_HOA_DON")
                {
                    if (string.IsNullOrWhiteSpace(req.MaHoaDon))
                        return Json(new { success = false, message = "Vui lòng chọn mã hóa đơn cần thanh toán." });

                    var hd = await _context.HoaDons
                        .Include(h => h.SinhVien)
                        .FirstOrDefaultAsync(h => h.MaHoaDon == req.MaHoaDon.Trim());

                    if (hd == null)
                        return Json(new { success = false, message = $"Không tìm thấy hóa đơn mã {req.MaHoaDon}." });

                    if (hd.TrangThai == "Đã thanh toán")
                        return Json(new { success = false, message = $"Hóa đơn {hd.MaHoaDon} đã được thanh toán trước đó." });

                    string phuongThuc = string.IsNullOrWhiteSpace(req.PhuongThuc) ? "Ví MoMo (Qua Trợ lý AI)" : req.PhuongThuc.Trim();
                    hd.TrangThai = "Đã thanh toán";
                    hd.HinhThucThanhToan = phuongThuc;
                    await _context.SaveChangesAsync();

                    return Json(new
                    {
                        success = true,
                        message = $"✅ Thanh toán thành công hóa đơn {hd.MaHoaDon}!",
                        receipt = new
                        {
                            maPhieu = hd.MaHoaDon,
                            loaiPhieu = $"Thanh toán {hd.LoaiHoaDon}",
                            maPhong = "P." + hd.MaPhong,
                            nguoiGui = $"{hd.MaSV} - {hd.SinhVien?.HoTen ?? sv.HoTen}",
                            noiDung = $"Số tiền: {hd.TongTien:N0} VNĐ",
                            chiTiet = $"Phương thức: {phuongThuc}",
                            trangThai = "Đã thanh toán",
                            thoiGian = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
                        }
                    });
                }

                return Json(new { success = false, message = "Loại thao tác chưa được hỗ trợ." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trong ChatbotController.ExecuteAction");
                return Json(new { success = false, message = "Lỗi xử lý hệ thống: " + ex.Message });
            }
        }

        private async Task<IActionResult> AskInternal(ChatRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
                return Json(new { response = "Bạn cần mình hỗ trợ thông tin gì về KTX?" });

            string rawMsg = request.Message.Trim();
            string msgLower = rawMsg.ToLower();

            // =====================================================================
            // 0. NHẬN DIỆN & XỬ LÝ TÁC VỤ HÀNH ĐỘNG (AI ACTION AGENT / FUNCTION CALLING)
            // =====================================================================
            string? actionIntent = DetectActionIntent(msgLower);
            if (!string.IsNullOrEmpty(actionIntent))
            {
                var actionResult = await HandleActionAgentAsync(actionIntent, rawMsg, msgLower);
                if (actionResult != null)
                    return actionResult;
            }

            // 1. Phân tích Intent bằng AI (nếu Ollama đang chạy)
            string classifyPrompt = $@"Phân tích câu hỏi và trả về duy nhất định dạng JSON thuần túy không kèm giải thích:
{{
  ""intent"": ""SINH_VIEN"" | ""PHONG"" | ""HOP_DONG"" | ""CO_SO_VAT_CHAT"" | ""KY_LUAT"" | ""SU_CO"" | ""THONG_KE"" | ""XIN_CHAO"" | ""HOADON_DIEN_NUOC"" | ""BAO_HONG_THIET_BI"" | ""GIOGIAC_NOIQUY"" | ""GUI_XE_RA_VAO"" | ""DANG_KY_TRA_PHONG"" | ""TIEP_KHACH_BAN_BE"" | ""KHAC"",
  ""keyword"": ""từ khóa nếu hỏi khái niệm, ngược lại để rỗng""
}}

Câu hỏi: ""{request.Message}""";

            string? jsonIntentResponse = await CallOllamaOrNullAsync(classifyPrompt);
            string intent = "KHAC";

            if (!string.IsNullOrWhiteSpace(jsonIntentResponse))
            {
                try
                {
                    string cleanJson = ExtractJsonString(jsonIntentResponse);
                    using var doc = JsonDocument.Parse(cleanJson);
                    if (doc.RootElement.TryGetProperty("intent", out var intentElem))
                        intent = intentElem.GetString() ?? "KHAC";
                }
                catch { }
            }

            // 1.1. Cơ chế nhận diện từ khóa ưu tiên (hỗ trợ cả khi Ollama chưa chạy)
            if (msgLower.Contains("xin chào") || msgLower == "chào" || msgLower == "hi" || msgLower == "hello")
                intent = "XIN_CHAO";
            else if (msgLower.Contains("giờ") || msgLower.Contains("giới nghiêm") || msgLower.Contains("đóng cửa") || msgLower.Contains("mở cửa") || msgLower.Contains("nội quy"))
                intent = "GIOGIAC_NOIQUY";
            else if (msgLower.Contains("báo hỏng") || msgLower.Contains("hướng dẫn báo") || msgLower.Contains("báo sự cố") || msgLower.Contains("báo cáo sự cố") || msgLower.Contains("báo cáo hỏng"))
                intent = "BAO_HONG_THIET_BI";
            else if (msgLower.Contains("điện nước") || msgLower.Contains("hóa đơn") || msgLower.Contains("giá điện") || msgLower.Contains("giá nước") || msgLower.Contains("thanh toán"))
                intent = "HOADON_DIEN_NUOC";
            else if (msgLower.Contains("gửi xe") || msgLower.Contains("bãi xe") || msgLower.Contains("xe máy") || msgLower.Contains("xe điện"))
                intent = "GUI_XE_RA_VAO";
            else if (msgLower.Contains("trả phòng") || msgLower.Contains("rời khỏi"))
                intent = "DANG_KY_TRA_PHONG";
            else if (msgLower.Contains("tiếp khách") || msgLower.Contains("bạn bè") || msgLower.Contains("người thân") || msgLower.Contains("qua đêm"))
                intent = "TIEP_KHACH_BAN_BE";
            else if (intent == "KHAC")
            {
                if (msgLower.Contains("thống kê") || msgLower.Contains("báo cáo") || msgLower.Contains("tổng số") || msgLower.Contains("bao nhiêu sinh viên") || msgLower.Contains("bao nhiêu phòng"))
                    intent = "THONG_KE";
                else if (msgLower.Contains("phòng") || msgLower.Contains("chỗ trống") || msgLower.Contains("loại phòng") || msgLower.Contains("giá phòng") || msgLower.Contains("xếp phòng") || msgLower.Contains("gợi ý"))
                    intent = "PHONG";
                else if (msgLower.Contains("sinh viên") || msgLower.Contains("người") || msgLower.Contains("tên") || msgLower.Contains("thành viên") || msgLower.Contains("mã sv") || msgLower.Contains("dtc"))
                    intent = "SINH_VIEN";
                else if (msgLower.Contains("hợp đồng") || msgLower.Contains("đăng ký") || msgLower.Contains("ngày hết hạn") || msgLower.Contains("gia hạn") || msgLower.Contains("thời hạn") || msgLower.Contains("tiền cọc"))
                    intent = "HOP_DONG";
                else if (msgLower.Contains("thiết bị") || msgLower.Contains("cơ sở vật chất") || msgLower.Contains("điều hòa") || msgLower.Contains("quạt") || msgLower.Contains("đèn") || msgLower.Contains("sửa") || msgLower.Contains("hỏng"))
                    intent = "CO_SO_VAT_CHAT";
                else if (msgLower.Contains("vi phạm") || msgLower.Contains("kỷ luật") || msgLower.Contains("phạt") || msgLower.Contains("lỗi"))
                    intent = "KY_LUAT";
                else if (msgLower.Contains("sự cố") || msgLower.Contains("phản ánh"))
                    intent = "SU_CO";
            }

            // 2. Tra cứu THỐNG KÊ TỔNG QUAN
            if (intent == "THONG_KE")
            {
                try
                {
                    var tongSV = await _context.SinhViens.CountAsync(s => s.TinhTrangLuuTru == "Đang ở");
                    var svDaRoi = await _context.SinhViens.CountAsync(s => s.TinhTrangLuuTru == "Đã rời đi");
                    var tongPhong = await _context.Phongs.CountAsync();
                    var phongConCho = await _context.Phongs.CountAsync(p => p.TinhTrang == "Còn chỗ");
                    var tongHopDong = await _context.HopDongs.CountAsync(h => h.TrangThai == "Còn hiệu lực");
                    var tongThietBi = await _context.CoSoVatChats.CountAsync();
                    var tongViPham = await _context.ViPhams.CountAsync();

                    var thongKeData = new
                    {
                        SinhVienDangO = tongSV,
                        SinhVienDaRoiDi = svDaRoi,
                        TongSoPhong = tongPhong,
                        PhongConCho = phongConCho,
                        HopDongHieuLuc = tongHopDong,
                        TongSoThietBi = tongThietBi,
                        TongSoTruongHopViPham = tongViPham
                    };

                    string prompt = $@"Dưới đây là số liệu thống kê tổng quan của KTX từ CSDL:\n{JsonSerializer.Serialize(thongKeData)}\n\nDựa vào dữ liệu trên, hãy trả lời câu hỏi bằng tiếng Việt rõ ràng, chính xác:\n""{request.Message}""";
                    string? aiRes = CleanAiOutput(await CallOllamaOrNullAsync(prompt));
                    string fallback = $"📊 Thống kê hệ thống KTX hiện tại:\n• Sinh viên đang ở: {tongSV} SV (Đã rời đi: {svDaRoi} SV)\n• Tổng số phòng: {tongPhong} phòng (Còn chỗ: {phongConCho} phòng, Đã đầy: {tongPhong - phongConCho} phòng)\n• Hợp đồng còn hiệu lực: {tongHopDong}\n• Thiết bị cơ sở vật chất: {tongThietBi} mục\n• Bản ghi vi phạm kỷ luật: {tongViPham} trường hợp";
                    return Json(new { response = aiRes ?? fallback, source = "Thống kê Hệ thống KTX" });
                }
                catch (Exception ex) { _logger.LogError(ex, "Lỗi Thống kê"); }
            }

            // 3. Tra cứu PHÒNG & GỢI Ý XẾP PHÒNG
            if (intent == "PHONG")
            {
                try
                {
                    var phongs = await _context.Phongs.Include(p => p.SinhViens).AsNoTracking().ToListAsync();
                    if (phongs != null && phongs.Any())
                    {
                        var phongConCho = phongs
                            .Select(p => new
                            {
                                p.MaPhong,
                                p.LoaiPhong,
                                p.SoLuongGiuong,
                                SoDangO = p.SinhViens?.Count(s => s.TinhTrangLuuTru == "Đang ở") ?? 0,
                                GiuongTrong = p.SoLuongGiuong - (p.SinhViens?.Count(s => s.TinhTrangLuuTru == "Đang ở") ?? 0),
                                p.GiaPhong,
                                p.TinhTrang
                            })
                            .Where(p => p.TinhTrang == "Còn chỗ" && p.GiuongTrong > 0)
                            .OrderByDescending(p => p.GiuongTrong)
                            .Take(10)
                            .ToList();

                        string prompt = $@"Dữ liệu các phòng còn chỗ trống trong KTX:\n{JsonSerializer.Serialize(phongConCho)}\n\nTrả lời câu hỏi sau bằng tiếng Việt chính xác, rõ ràng:\n""{request.Message}""";
                        string? aiRes = CleanAiOutput(await CallOllamaOrNullAsync(prompt));
                        if (!string.IsNullOrWhiteSpace(aiRes))
                            return Json(new { response = aiRes, source = "Quản lý Phòng" });

                        if (msgLower.Contains("giá"))
                        {
                            string giaMsg = "💰 Bảng giá phòng Ký túc xá niêm yết:\n• Phòng thường: 9.000.000 VNĐ / kỳ\n• Phòng có điều hoà: 11.000.000 VNĐ / kỳ\n• Phòng điều hoà + nóng lạnh: 13.000.000 VNĐ / kỳ\n• Hiện có " + phongs.Count(p => p.TinhTrang == "Còn chỗ") + "/" + phongs.Count + " phòng đang còn chỗ trống.";
                            return Json(new { response = giaMsg, source = "Quản lý Phòng" });
                        }

                        var goiY = string.Join("\n", phongConCho.Take(6).Select(p => $"• Phòng P.{p.MaPhong} ({p.LoaiPhong}): Còn trống {p.GiuongTrong}/{p.SoLuongGiuong} giường — Giá: {p.GiaPhong:N0}đ"));
                        string fallback = $"🏢 Hệ thống hiện có {phongs.Count(p => p.TinhTrang == "Còn chỗ")}/{phongs.Count} phòng còn chỗ trống.\nGợi ý các phòng còn nhiều giường trống nhất:\n{goiY}";
                        return Json(new { response = fallback, source = "Quản lý Phòng" });
                    }
                }
                catch (Exception ex) { _logger.LogError(ex, "Lỗi CSDL Phòng"); }
            }

            // 4. Tra cứu SINH VIÊN
            if (intent == "SINH_VIEN")
            {
                try
                {
                    var matchMaSV = Regex.Match(request.Message.ToUpper(), @"DTC\d{3}");
                    if (matchMaSV.Success)
                    {
                        string ma = matchMaSV.Value;
                        var sv = await _context.SinhViens.AsNoTracking().FirstOrDefaultAsync(s => s.MaSV == ma);
                        if (sv != null)
                        {
                            return Json(new { response = $"👨‍🎓 Thông tin sinh viên {sv.MaSV}:\n• Họ tên: {sv.HoTen}\n• Lớp: {sv.Lop}\n• Phòng: P.{sv.MaPhong ?? "Chưa xếp"}\n• Tình trạng: {sv.TinhTrangLuuTru}", source = "Quản lý Sinh viên" });
                        }
                    }

                    // Bảo mật quyền riêng tư: Chỉ chọn các trường công khai, ẩn CCCD và Số điện thoại trước khi gửi sang AI
                    var data = await _context.SinhViens
                        .Where(s => s.TinhTrangLuuTru == "Đang ở")
                        .Select(s => new
                        {
                            s.MaSV,
                            s.HoTen,
                            s.Lop,
                            s.MaPhong,
                            s.TinhTrangLuuTru
                        })
                        .Take(20)
                        .AsNoTracking()
                        .ToListAsync();
                    var tongDangO = await _context.SinhViens.CountAsync(s => s.TinhTrangLuuTru == "Đang ở");
                    if (data != null && data.Any())
                    {
                        string prompt = $@"Dữ liệu mẫu Sinh viên đang ở trong KTX (Tổng: {tongDangO} SV):\n{JsonSerializer.Serialize(data)}\n\nTrả lời câu hỏi sau bằng tiếng Việt ngắn gọn:\n""{request.Message}""";
                        string? aiRes = CleanAiOutput(await CallOllamaOrNullAsync(prompt));
                        string fallback = $"👨‍🎓 Hiện KTX có {tongDangO} sinh viên đang lưu trú.\nMột số sinh viên tiêu biểu:\n" + string.Join("\n", data.Take(5).Select(s => $"• {s.MaSV} - {s.HoTen} (Lớp {s.Lop}, Phòng P.{s.MaPhong})"));
                        return Json(new { response = aiRes ?? fallback, source = "Quản lý Sinh viên" });
                    }
                }
                catch (Exception ex) { _logger.LogError(ex, "Lỗi CSDL Sinh viên"); }
            }

            // 5. Tra cứu HỢP ĐỒNG
            if (intent == "HOP_DONG")
            {
                try
                {
                    var data = await _context.HopDongs.AsNoTracking().Take(15).ToListAsync();
                    int conHieuLuc = await _context.HopDongs.CountAsync(h => h.TrangThai == "Còn hiệu lực");
                    if (data != null && data.Any())
                    {
                        string prompt = $@"Dữ liệu Hợp đồng KTX:\n{JsonSerializer.Serialize(data)}\n\nTrả lời câu hỏi của người dùng bằng tiếng Việt ngắn gọn:\n""{request.Message}""";
                        string? aiRes = CleanAiOutput(await CallOllamaOrNullAsync(prompt));
                        string fallback = $"📄 Hệ thống hiện có {conHieuLuc} hợp đồng đang còn hiệu lực (Tiền đặt cọc tiêu chuẩn: 1.500.000 VNĐ/hợp đồng). Sinh viên cần tuân thủ nội quy KTX và đóng phí đúng hạn.";
                        return Json(new { response = aiRes ?? fallback, source = "Quản lý Hợp đồng" });
                    }
                }
                catch (Exception ex) { _logger.LogError(ex, "Lỗi CSDL Hợp đồng"); }
            }

            // 6. Tra cứu CƠ SỞ VẬT CHẤT
            if (intent == "CO_SO_VAT_CHAT")
            {
                try
                {
                    var data = await _context.CoSoVatChats.AsNoTracking().ToListAsync();
                    if (data != null && data.Any())
                    {
                        string prompt = $@"Dữ liệu Cơ sở vật chất / Thiết bị KTX:\n{JsonSerializer.Serialize(data.Take(20))}\n\nTrả lời câu hỏi bằng tiếng Việt chính xác theo tình trạng thiết bị:\n""{request.Message}""";
                        string? aiRes = CleanAiOutput(await CallOllamaOrNullAsync(prompt));
                        var canBaoTri = data.Where(c => c.TinhTrang != "Tốt" && c.TinhTrang != "Đang sử dụng").ToList();
                        string fallback = $"🛠 KTX hiện quản lý {data.Count} danh mục thiết bị tại các phòng.\n• Thiết bị hoạt động tốt: {data.Count - canBaoTri.Count}\n• Thiết bị cần bảo trì/sửa chữa: {canBaoTri.Count}" + (canBaoTri.Any() ? " (" + string.Join(", ", canBaoTri.Take(5).Select(c => $"{c.TenThietBi} P.{c.MaPhong}")) + ")" : "");
                        return Json(new { response = aiRes ?? fallback, source = "Quản lý Cơ sở vật chất" });
                    }
                }
                catch (Exception ex) { _logger.LogError(ex, "Lỗi CSDL Cơ sở vật chất"); }
            }

            // 7. Tra cứu KỶ LUẬT / VI PHẠM
            if (intent == "KY_LUAT")
            {
                try
                {
                    var data = await _context.ViPhams.AsNoTracking().OrderByDescending(v => v.NgayViPham).Take(10).ToListAsync();
                    if (data != null && data.Any())
                    {
                        string prompt = $@"Dữ liệu Sinh viên Vi phạm Kỷ luật KTX:\n{JsonSerializer.Serialize(data)}\n\nTrả lời câu hỏi bằng tiếng Việt rõ ràng, ngắn gọn:\n""{request.Message}""";
                        string? aiRes = CleanAiOutput(await CallOllamaOrNullAsync(prompt));
                        string fallback = $"⚠️ Hệ thống ghi nhận {await _context.ViPhams.CountAsync()} trường hợp vi phạm nội quy.\nCác lỗi phổ biến: " + string.Join("; ", data.Take(4).Select(v => $"SV {v.MaSV} ({v.NoiDungViPham} - {v.HinhThucXuLy})"));
                        return Json(new { response = aiRes ?? fallback, source = "Quản lý Kỷ luật" });
                    }
                }
                catch (Exception ex) { _logger.LogError(ex, "Lỗi CSDL Kỷ luật"); }
            }

            // 7.5. Tra cứu SỰ CỐ / PHẢN ÁNH
            if (intent == "SU_CO")
            {
                try
                {
                    var data = await _context.BaoCaoSuCos.Where(b => b.TrangThai == "Chờ xử lý").AsNoTracking().ToListAsync();
                    if (data != null && data.Any())
                    {
                        string prompt = $@"Bạn là trợ lý AI quản lý KTX. Dưới đây là dữ liệu các sự cố KTX đang chờ xử lý:\n{JsonSerializer.Serialize(data)}\n\nHãy trả lời câu hỏi sau bằng tiếng Việt. Nếu người dùng yêu cầu tóm tắt, hãy phân loại và tóm tắt chúng rõ ràng:\n""{request.Message}""";
                        string? aiRes = CleanAiOutput(await CallOllamaOrNullAsync(prompt));
                        string fallback = $"🔧 Hiện có {data.Count} báo cáo sự cố đang chờ xử lý:\n" + string.Join("\n", data.Take(5).Select(b => $"• P.{b.MaPhong}: {b.TenSuCo} (Mức độ: {b.MucDoKhanCap})"));
                        return Json(new { response = aiRes ?? fallback, source = "Quản lý Sự cố" });
                    }
                    else
                    {
                        return Json(new { response = "Hiện không có báo cáo sự cố nào đang chờ xử lý.", source = "Quản lý Sự cố" });
                    }
                }
                catch (Exception ex) { _logger.LogError(ex, "Lỗi CSDL Sự cố"); }
            }

            // 8. Tra cứu Quy định chung từ file JSON Knowledge Base (wwwroot/data/knowledge_base.json)
            string matchedFacts = GetFactsByIntent(intent);
            string finalPrompt = AIPrompts.BuildPolicyRagPrompt(request.Message, matchedFacts);

            string? aiResponse = CleanAiOutput(await CallOllamaOrNullAsync(finalPrompt));

            // Đính kèm gợi ý nút hành động nhanh cho các chủ đề có hỗ trợ thực thi tự động
            object? quickActionHint = null;
            if (intent == "BAO_HONG_THIET_BI")
                quickActionHint = new { label = "⚡ Nhờ AI tạo Báo cáo sự cố hộ ngay", prompt = "Báo cáo sự cố hộ tôi" };
            else if (intent == "DANG_KY_TRA_PHONG")
                quickActionHint = new { label = "⚡ Nhờ AI gửi đơn Chuyển / Gia hạn / Trả phòng", prompt = "Làm đơn chuyển hoặc trả phòng hộ tôi" };
            else if (intent == "HOADON_DIEN_NUOC")
                quickActionHint = new { label = "⚡ Tra cứu & Thanh toán hóa đơn ngay", prompt = "Thanh toán hóa đơn hộ tôi" };

            return Json(new { response = aiResponse ?? matchedFacts, source = "Cơ sở tri thức KTX", quickAction = quickActionHint });
        }

        // =========================================================================
        // BỘ NHẬN DIỆN Ý ĐỊNH HÀNH ĐỘNG (AI ACTION ROUTER)
        // =========================================================================
        private string? DetectActionIntent(string msgLower)
        {
            // Kiểm tra xem có phải câu hỏi hỏi đáp thông tin thuần túy (hướng dẫn, bao nhiêu tiền, quy định...) không
            bool isPureQuestion = (msgLower.StartsWith("hướng dẫn") || msgLower.StartsWith("quy định") ||
                                   msgLower.StartsWith("thủ tục") || msgLower.StartsWith("giá ") ||
                                   msgLower.Contains("bao nhiêu tiền") || msgLower.Contains("mấy giờ"))
                                  && !ContainsDelegationKeyword(msgLower);

            if (isPureQuestion)
                return null;

            bool hasDelegation = ContainsDelegationKeyword(msgLower);

            // 1. Hỏi tổng quát về khả năng làm hộ / tự động thực hiện các chức năng
            if ((msgLower.Contains("chức năng khác") || msgLower.Contains("làm được gì") ||
                 msgLower.Contains("làm hộ được gì") || msgLower.Contains("thao tác thủ công") ||
                 msgLower.Contains("thực hiện hộ") || msgLower.Contains("làm thay") ||
                 msgLower.Contains("action agent") || msgLower.Contains("function calling") ||
                 msgLower == "nhờ ai làm hộ" || msgLower == "làm hộ tôi") &&
                !msgLower.Contains("sự cố") && !msgLower.Contains("báo hỏng"))
            {
                return "ACTION_HUB";
            }

            // 2. Hành động Báo cáo sự cố / Báo hỏng hộ
            if ((hasDelegation && (msgLower.Contains("báo cáo") || msgLower.Contains("báo hỏng") ||
                                   msgLower.Contains("báo sự cố") || msgLower.Contains("sự cố") ||
                                   msgLower.Contains("sửa") || msgLower.Contains("hỏng") ||
                                   msgLower.Contains("cháy") || msgLower.Contains("rò rỉ") ||
                                   msgLower.Contains("tắc") || msgLower.Contains("mất điện") ||
                                   msgLower.Contains("mất nước") || msgLower.Contains("đèn") ||
                                   msgLower.Contains("quạt") || msgLower.Contains("điều hòa") ||
                                   msgLower.Contains("vòi") || msgLower.Contains("khóa") ||
                                   msgLower.Contains("cửa") || msgLower.Contains("ổ cắm") ||
                                   msgLower.Contains("nóng lạnh") || msgLower.Contains("wifi"))) ||
                msgLower.Contains("báo cáo hộ") || msgLower.Contains("báo hộ") ||
                msgLower.Contains("báo hỏng giúp") || msgLower.Contains("báo sự cố giúp") ||
                msgLower.Contains("tạo báo cáo sự cố") || msgLower.Contains("gửi báo cáo sự cố") ||
                Regex.IsMatch(msgLower, @"(phòng|p\.?\s*\d+).*(bị hỏng|hỏng|cháy|rò rỉ|mất điện|mất nước|tắc)") ||
                Regex.IsMatch(msgLower, @"^báo hỏng\s+\w+"))
            {
                return "ACTION_BAO_CAO_SU_CO";
            }

            // 3. Hành động Xin chuyển phòng / Đổi phòng
            if ((hasDelegation && (msgLower.Contains("chuyển phòng") || msgLower.Contains("đổi phòng") ||
                                   msgLower.Contains("chuyển sang phòng") || msgLower.Contains("đổi sang phòng"))) ||
                msgLower.Contains("xin chuyển phòng") || msgLower.Contains("muốn chuyển phòng") ||
                msgLower.Contains("muốn đổi phòng") || msgLower.Contains("làm đơn chuyển phòng") ||
                Regex.IsMatch(msgLower, @"(chuyển|đổi)\s+(từ\s+phòng.*)?sang\s+phòng"))
            {
                return "ACTION_CHUYEN_PHONG";
            }

            // 4. Hành động Đăng ký gia hạn hợp đồng / gia hạn ở
            if ((hasDelegation && (msgLower.Contains("gia hạn") || msgLower.Contains("hợp đồng"))) ||
                msgLower.Contains("xin gia hạn") || msgLower.Contains("muốn gia hạn") ||
                msgLower.Contains("đăng ký gia hạn") || msgLower.Contains("làm đơn gia hạn"))
            {
                return "ACTION_GIA_HAN";
            }

            // 5. Hành động Đăng ký trả phòng
            if ((hasDelegation && (msgLower.Contains("trả phòng") || msgLower.Contains("rời khỏi"))) ||
                msgLower.Contains("xin trả phòng") || msgLower.Contains("muốn trả phòng") ||
                msgLower.Contains("đăng ký trả phòng") || msgLower.Contains("làm đơn trả phòng"))
            {
                return "ACTION_TRA_PHONG";
            }

            // 6. Hành động Xem & Thanh toán hóa đơn
            if ((hasDelegation && (msgLower.Contains("hóa đơn") || msgLower.Contains("thanh toán") ||
                                   msgLower.Contains("đóng tiền") || msgLower.Contains("tiền điện") ||
                                   msgLower.Contains("tiền nước") || msgLower.Contains("tiền phòng"))) ||
                msgLower.Contains("hóa đơn của tôi") || msgLower.Contains("hóa đơn phòng tôi") ||
                msgLower.Contains("thanh toán hóa đơn") || msgLower.Contains("thanh toán hộ") ||
                msgLower.Contains("đóng tiền điện") || msgLower.Contains("đóng tiền phòng") ||
                msgLower.Contains("tôi còn nợ"))
            {
                return "ACTION_HOA_DON";
            }

            // 7. Hành động Kiểm tra tiến độ đơn / lịch sử báo cáo của bản thân
            if (msgLower.Contains("đơn của tôi") || msgLower.Contains("báo cáo của tôi") ||
                msgLower.Contains("yêu cầu của tôi") || msgLower.Contains("kiểm tra tiến độ") ||
                msgLower.Contains("duyệt chưa"))
            {
                return "ACTION_TRA_CUU_DON";
            }

            // 8. Nếu người dùng hỏi chung chung có từ khóa nhờ làm hộ ("bạn có thể làm hộ tôi không", "giúp tôi thực hiện...")
            if (hasDelegation && (msgLower.Contains("làm") || msgLower.Contains("thực hiện") || msgLower.Contains("thao tác")))
            {
                return "ACTION_HUB";
            }

            return null;
        }

        private bool ContainsDelegationKeyword(string msgLower)
        {
            string[] keywords = new[]
            {
                "hộ", "giúp", "giùm", "thay tôi", "thay mình", "nhờ ", "làm hộ", "làm giúp",
                "báo hộ", "báo giúp", "gửi hộ", "gửi giúp", "tạo hộ", "tạo giúp",
                "đăng ký hộ", "đăng ký giúp", "thanh toán hộ", "thanh toán giúp",
                "co the bao", "có thể báo", "tự động", "ngay tại đây", "luôn giúp"
            };
            return keywords.Any(k => msgLower.Contains(k));
        }

        // =========================================================================
        // XỬ LÝ TÁC VỤ AI ACTION AGENT (TỰ ĐỘNG THỰC THI HOẶC HIỂN THỊ ACTION CARD)
        // =========================================================================
        private async Task<IActionResult?> HandleActionAgentAsync(string actionIntent, string rawMsg, string msgLower)
        {
            string sessionMaSV = HttpContext.Session.GetString("MaSV_DangNhap") ?? "";
            SinhVien? currentSv = null;

            if (!string.IsNullOrWhiteSpace(sessionMaSV))
            {
                currentSv = await _context.SinhViens.AsNoTracking().FirstOrDefaultAsync(s => s.MaSV == sessionMaSV);
            }

            // Kiểm tra nếu tin nhắn có nhắc trực tiếp mã SV (VD: DTC001)
            var matchSv = Regex.Match(rawMsg.ToUpper(), @"DTC\d{3}");
            if (matchSv.Success)
            {
                var mentionedSv = await _context.SinhViens.AsNoTracking().FirstOrDefaultAsync(s => s.MaSV == matchSv.Value);
                if (mentionedSv != null)
                    currentSv = mentionedSv;
            }

            var allRooms = await _context.Phongs.AsNoTracking().OrderBy(p => p.MaPhong).ToListAsync();
            var mentionedRooms = ExtractRoomsFromMessage(rawMsg, allRooms);

            string activeMaPhong = mentionedRooms.FirstOrDefault() ?? currentSv?.MaPhong ?? "";
            if (currentSv == null && !string.IsNullOrWhiteSpace(activeMaPhong))
            {
                currentSv = await _context.SinhViens.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.MaPhong == activeMaPhong && s.TinhTrangLuuTru == "Đang ở")
                    ?? await _context.SinhViens.AsNoTracking().FirstOrDefaultAsync(s => s.MaPhong == activeMaPhong);
            }

            var roomOptions = allRooms.Select(p => new
            {
                maPhong = p.MaPhong,
                loaiPhong = p.LoaiPhong,
                tinhTrang = p.TinhTrang,
                giaPhong = p.GiaPhong
            }).ToList();

            // ---------------------------------------------------------------------
            // A. MENU TỔNG HỢP TRỢ LÝ HÀNH ĐỘNG (ACTION HUB)
            // ---------------------------------------------------------------------
            if (actionIntent == "ACTION_HUB")
            {
                string svInfo = currentSv != null
                    ? $" (Đang kết nối hồ sơ: **{currentSv.HoTen} - {currentSv.MaSV}**, Phòng **P.{currentSv.MaPhong}**)"
                    : "";

                string hubMsg = $"🤖 **Trợ lý AI Hành động (AI Action Agent)** sẵn sàng phục vụ bạn!{svInfo}\n\n" +
                                "Không chỉ giải đáp thắc mắc, tôi có thể **thay mặt bạn thực hiện trực tiếp các thao tác trên hệ thống** mà bạn không cần phải chuyển trang hay nhập biểu mẫu thủ công:\n" +
                                "• 🛠 **Báo hỏng / Báo cáo sự cố hộ:** Tự động lập phiếu gửi đội kỹ thuật.\n" +
                                "• 🔄 **Xin chuyển phòng:** Tự động kiểm tra phòng trống và gửi đơn chuyển phòng.\n" +
                                "• 📅 **Gia hạn hợp đồng / lưu trú:** Gửi yêu cầu gia hạn kỳ tiếp theo.\n" +
                                "• 🚪 **Đăng ký trả phòng:** Gửi đơn báo trả phòng lên Ban Quản lý.\n" +
                                "• 💳 **Tra cứu & Thanh toán hóa đơn:** Thanh toán tiền phòng, điện nước ngay trong chat.\n\n" +
                                "👇 **Bạn hãy bấm chọn thao tác muốn tôi làm hộ bên dưới (hoặc gõ lệnh trực tiếp):**";

                return Json(new
                {
                    response = hubMsg,
                    source = "AI Action Agent (Trợ lý Thực thi)",
                    actionCard = new
                    {
                        cardType = "ACTION_HUB",
                        currentMaSV = currentSv?.MaSV ?? "",
                        currentHoTen = currentSv?.HoTen ?? "",
                        currentMaPhong = activeMaPhong
                    }
                });
            }

            // ---------------------------------------------------------------------
            // B. BÁO CÁO SỰ CỐ / BÁO HỎNG THIẾT BỊ HỘ
            // ---------------------------------------------------------------------
            if (actionIntent == "ACTION_BAO_CAO_SU_CO")
            {
                var (hasSpecificIssue, extractedTitle, extractedUrgency) = ExtractIncidentDetails(rawMsg, msgLower);

                // Nếu người dùng đã nêu rõ thiết bị hỏng VÀ đã xác định được phòng -> TỰ ĐỘNG THỰC THI LUÔN!
                if (hasSpecificIssue && !string.IsNullOrWhiteSpace(activeMaPhong))
                {
                    var targetSv = currentSv
                        ?? await _context.SinhViens.FirstOrDefaultAsync(s => s.MaPhong == activeMaPhong && s.TinhTrangLuuTru == "Đang ở")
                        ?? await _context.SinhViens.FirstOrDefaultAsync();

                    if (targetSv != null)
                    {
                        string maSC = "SC_" + DateTime.Now.ToString("yyyyMMddHHmmss");
                        var bc = new BaoCaoSuCo
                        {
                            MaSuCo = maSC,
                            TenSuCo = extractedTitle,
                            MoTa = $"[Tự động qua Trợ lý AI] {rawMsg}",
                            MucDoKhanCap = extractedUrgency,
                            HinhAnh = "",
                            MaSV = targetSv.MaSV,
                            MaPhong = activeMaPhong,
                            TrangThai = "Chờ xử lý",
                            NgayBaoCao = DateTime.Now
                        };

                        _context.BaoCaoSuCos.Add(bc);
                        await _context.SaveChangesAsync();

                        string autoMsg = $"✅ **Đã tự động tạo báo cáo sự cố thành công!**\n" +
                                         $"Tôi đã thay mặt bạn ghi nhận sự cố **\"{extractedTitle}\"** cho phòng **P.{activeMaPhong}** và chuyển thẳng tới Ban Quản lý / Đội kỹ thuật KTX.";

                        return Json(new
                        {
                            response = autoMsg,
                            source = "AI Action Agent (Đã thực thi tự động)",
                            actionCard = new
                            {
                                cardType = "RECEIPT_CARD",
                                receipt = new
                                {
                                    maPhieu = maSC,
                                    loaiPhieu = "Báo cáo sự cố kỹ thuật",
                                    maPhong = "P." + activeMaPhong,
                                    nguoiGui = $"{targetSv.MaSV} - {targetSv.HoTen}",
                                    noiDung = extractedTitle,
                                    chiTiet = $"Mức độ: {extractedUrgency} | Nội dung: {rawMsg}",
                                    trangThai = "Chờ xử lý",
                                    thoiGian = bc.NgayBaoCao.ToString("dd/MM/yyyy HH:mm")
                                }
                            }
                        });
                    }
                }

                // Nếu người dùng mới hỏi "bạn có thể báo cáo hộ tôi không" hoặc chưa nêu rõ phòng/sự cố -> Mở Thẻ tương tác điền sẵn 1-Click
                string replyMsg = "🛠 **Hoàn toàn ĐƯỢC! Tôi có thể tạo và gửi báo cáo sự cố hộ bạn ngay tại đây.**\n" +
                                  "• **Cách 1 (1-Click):** Chọn nhanh loại sự cố trên thẻ bên dưới và bấm **Gửi báo cáo ngay**.\n" +
                                  "• **Cách 2 (Ra lệnh bằng câu nói):** Nhắn trực tiếp kiểu *\"Báo hỏng vòi nước phòng 101 giúp tôi\"*, tôi sẽ tự động tạo phiếu ngay lập tức mà bạn không cần điền form!";

                return Json(new
                {
                    response = replyMsg,
                    source = "AI Action Agent (Báo cáo sự cố)",
                    actionCard = new
                    {
                        cardType = "FORM_BAO_CAO_SU_CO",
                        currentMaSV = currentSv?.MaSV ?? "",
                        currentHoTen = currentSv?.HoTen ?? "",
                        currentMaPhong = activeMaPhong,
                        defaultTenSuCo = hasSpecificIssue ? extractedTitle : "",
                        defaultMucDo = extractedUrgency,
                        defaultMoTa = hasSpecificIssue ? rawMsg : "",
                        rooms = roomOptions
                    }
                });
            }

            // ---------------------------------------------------------------------
            // C. XIN CHUYỂN PHÒNG / ĐĂNG KÝ GIA HẠN / ĐĂNG KÝ TRẢ PHÒNG
            // ---------------------------------------------------------------------
            if (actionIntent == "ACTION_CHUYEN_PHONG" || actionIntent == "ACTION_GIA_HAN" || actionIntent == "ACTION_TRA_PHONG")
            {
                string defaultLoai = actionIntent == "ACTION_CHUYEN_PHONG"
                    ? "Xin chuyển phòng"
                    : (actionIntent == "ACTION_GIA_HAN" ? "Đăng ký gia hạn ở" : "Đăng ký trả phòng");

                string sourceRoom = currentSv?.MaPhong ?? "";
                string targetRoom = "";

                if (actionIntent == "ACTION_CHUYEN_PHONG")
                {
                    if (mentionedRooms.Count >= 2)
                    {
                        sourceRoom = mentionedRooms[0];
                        targetRoom = mentionedRooms[1];
                    }
                    else if (mentionedRooms.Count == 1)
                    {
                        if (!string.IsNullOrWhiteSpace(sourceRoom) && !sourceRoom.Equals(mentionedRooms[0], StringComparison.OrdinalIgnoreCase))
                        {
                            targetRoom = mentionedRooms[0];
                        }
                        else if (Regex.IsMatch(msgLower, @"(sang|đến|tới|qua)\s+phòng\s*" + Regex.Escape(mentionedRooms[0].ToLower())))
                        {
                            targetRoom = mentionedRooms[0];
                        }
                        else
                        {
                            targetRoom = mentionedRooms[0];
                        }
                    }
                }
                else
                {
                    if (mentionedRooms.Any())
                        sourceRoom = mentionedRooms[0];
                }

                // Kiểm tra điều kiện TỰ ĐỘNG THỰC THI LUÔN (Auto-Execute)
                bool canAutoExecute = false;
                if (actionIntent == "ACTION_CHUYEN_PHONG" && !string.IsNullOrWhiteSpace(sourceRoom) && !string.IsNullOrWhiteSpace(targetRoom) && !sourceRoom.Equals(targetRoom, StringComparison.OrdinalIgnoreCase))
                {
                    canAutoExecute = true;
                }
                else if ((actionIntent == "ACTION_GIA_HAN" || actionIntent == "ACTION_TRA_PHONG") && !string.IsNullOrWhiteSpace(sourceRoom) &&
                         (mentionedRooms.Any() || msgLower.Contains("giúp tôi") || msgLower.Contains("hộ tôi") || msgLower.Contains("giúp mình") || msgLower.Contains("hộ mình") || msgLower.Contains("ngay")))
                {
                    canAutoExecute = true;
                }

                if (canAutoExecute)
                {
                    var targetSv = currentSv
                        ?? await _context.SinhViens.FirstOrDefaultAsync(s => s.MaPhong == sourceRoom && s.TinhTrangLuuTru == "Đang ở")
                        ?? await _context.SinhViens.FirstOrDefaultAsync();

                    if (targetSv != null)
                    {
                        string phongMongMuon = actionIntent == "ACTION_CHUYEN_PHONG"
                            ? $"Từ phòng {sourceRoom} -> Chuyển sang phòng {targetRoom}"
                            : $"Phòng hiện tại: {sourceRoom}";

                        string maYC = "YC_" + DateTime.Now.ToString("yyyyMMddHHmmss");
                        var yc = new BaoCaoSuCo
                        {
                            MaSuCo = maYC,
                            TenSuCo = $"[{defaultLoai}] Nguyện vọng: {phongMongMuon}",
                            MoTa = $"[Tự động qua Trợ lý AI] {rawMsg}",
                            MucDoKhanCap = "Bình thường",
                            HinhAnh = "",
                            MaSV = targetSv.MaSV,
                            MaPhong = sourceRoom,
                            TrangThai = "Chờ xử lý",
                            NgayBaoCao = DateTime.Now
                        };

                        _context.BaoCaoSuCos.Add(yc);
                        await _context.SaveChangesAsync();

                        return Json(new
                        {
                            response = $"✅ **Đã tự động gửi đơn '{defaultLoai}' thành công!**\nTôi đã thay mặt bạn lập hồ sơ **{maYC}** ({phongMongMuon}) và gửi tới Ban Quản lý phê duyệt.",
                            source = "AI Action Agent (Đã thực thi tự động)",
                            actionCard = new
                            {
                                cardType = "RECEIPT_CARD",
                                receipt = new
                                {
                                    maPhieu = maYC,
                                    loaiPhieu = defaultLoai,
                                    maPhong = "P." + sourceRoom,
                                    nguoiGui = $"{targetSv.MaSV} - {targetSv.HoTen}",
                                    noiDung = phongMongMuon,
                                    chiTiet = $"Nội dung: {rawMsg}",
                                    trangThai = "Chờ Ban Quản lý duyệt",
                                    thoiGian = yc.NgayBaoCao.ToString("dd/MM/yyyy HH:mm")
                                }
                            }
                        });
                    }
                }

                // Hiển thị Action Card nếu chưa đủ tham số phòng
                var availableRooms = allRooms
                    .Where(p => p.TinhTrang == "Còn chỗ")
                    .Select(p => new { maPhong = p.MaPhong, loaiPhong = p.LoaiPhong, giaPhong = p.GiaPhong })
                    .ToList();

                return Json(new
                {
                    response = $"📋 **Tôi có thể gửi đơn \"{defaultLoai}\" hộ bạn ngay tại khung chat này!**\n" +
                               "Bạn hãy kiểm tra thông tin phòng bên dưới và bấm **Gửi yêu cầu ngay** (hoặc nhắn kiểu: *\"Chuyển từ phòng 101 sang phòng 102 giúp tôi\"* để tôi tự động gửi):",
                    source = $"AI Action Agent ({defaultLoai})",
                    actionCard = new
                    {
                        cardType = "FORM_YEU_CAU_PHONG",
                        currentMaSV = currentSv?.MaSV ?? "",
                        currentHoTen = currentSv?.HoTen ?? "",
                        currentMaPhong = sourceRoom,
                        defaultLoaiYeuCau = defaultLoai,
                        defaultPhongChuyenDen = targetRoom,
                        defaultLyDo = rawMsg,
                        rooms = roomOptions,
                        availableRooms = availableRooms
                    }
                });
            }

            // ---------------------------------------------------------------------
            // D. TRA CỨU & THANH TOÁN HÓA ĐƠN TRỰC TIẾP QUA CHAT
            // ---------------------------------------------------------------------
            if (actionIntent == "ACTION_HOA_DON")
            {
                var query = _context.HoaDons.Include(h => h.SinhVien).AsQueryable();
                if (currentSv != null)
                {
                    query = query.Where(h => h.MaSV == currentSv.MaSV || h.MaPhong == currentSv.MaPhong);
                }
                else if (!string.IsNullOrWhiteSpace(activeMaPhong))
                {
                    query = query.Where(h => h.MaPhong == activeMaPhong);
                }

                var hoaDons = await query.OrderByDescending(h => h.NgayLap).Take(10).ToListAsync();
                var chuaThanhToan = hoaDons.Where(h => h.TrangThai != "Đã thanh toán").ToList();

                // Nếu người dùng yêu cầu thanh toán luôn và có hóa đơn chưa thanh toán
                if (chuaThanhToan.Any() && (msgLower.Contains("thanh toán luôn") || msgLower.Contains("thanh toán ngay") || msgLower.Contains("đóng luôn")))
                {
                    var hdToPay = chuaThanhToan.First();
                    hdToPay.TrangThai = "Đã thanh toán";
                    hdToPay.HinhThucThanhToan = "Ví điện tử (Qua Trợ lý AI)";
                    await _context.SaveChangesAsync();

                    return Json(new
                    {
                        response = $"✅ **Đã tự động thanh toán hóa đơn {hdToPay.MaHoaDon} ({hdToPay.LoaiHoaDon}) thành công!**",
                        source = "AI Action Agent (Thanh toán tự động)",
                        actionCard = new
                        {
                            cardType = "RECEIPT_CARD",
                            receipt = new
                            {
                                maPhieu = hdToPay.MaHoaDon,
                                loaiPhieu = $"Thanh toán {hdToPay.LoaiHoaDon}",
                                maPhong = "P." + hdToPay.MaPhong,
                                nguoiGui = $"{hdToPay.MaSV} - {hdToPay.SinhVien?.HoTen}",
                                noiDung = $"{hdToPay.TongTien:N0} VNĐ",
                                chiTiet = "Phương thức: Ví điện tử (Qua Trợ lý AI)",
                                trangThai = "Đã thanh toán",
                                thoiGian = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
                            }
                        }
                    });
                }

                var invoiceItems = (chuaThanhToan.Any() ? chuaThanhToan : hoaDons.Take(5)).Select(h => new
                {
                    maHoaDon = h.MaHoaDon,
                    loaiHoaDon = h.LoaiHoaDon,
                    maPhong = h.MaPhong,
                    maSV = h.MaSV,
                    hoTen = h.SinhVien?.HoTen ?? "",
                    tongTien = h.TongTien,
                    trangThai = h.TrangThai,
                    ngayLap = h.NgayLap.ToString("dd/MM/yyyy")
                }).ToList();

                string billMsg = chuaThanhToan.Any()
                    ? $"💳 Hiện có **{chuaThanhToan.Count} hóa đơn chưa thanh toán** (Tổng cộng: **{chuaThanhToan.Sum(x => x.TongTien):N0} VNĐ**).\nBạn có thể bấm **Thanh toán ngay** trên thẻ dưới đây để tôi gạch nợ trực tiếp giúp bạn:"
                    : "✅ Hiện tại không có hóa đơn nào còn nợ. Dưới đây là lịch sử hóa đơn gần nhất trên hệ thống:";

                return Json(new
                {
                    response = billMsg,
                    source = "AI Action Agent (Quản lý Hóa đơn)",
                    actionCard = new
                    {
                        cardType = "FORM_THANH_TOAN",
                        currentMaSV = currentSv?.MaSV ?? "",
                        currentHoTen = currentSv?.HoTen ?? "",
                        currentMaPhong = activeMaPhong,
                        invoices = invoiceItems
                    }
                });
            }

            // ---------------------------------------------------------------------
            // E. TRA CỨU TIẾN ĐỘ ĐƠN / BÁO CÁO CỦA BẢN THÂN
            // ---------------------------------------------------------------------
            if (actionIntent == "ACTION_TRA_CUU_DON")
            {
                var query = _context.BaoCaoSuCos.AsNoTracking().AsQueryable();
                if (currentSv != null)
                {
                    query = query.Where(b => b.MaSV == currentSv.MaSV || b.MaPhong == currentSv.MaPhong);
                }
                else if (!string.IsNullOrWhiteSpace(activeMaPhong))
                {
                    query = query.Where(b => b.MaPhong == activeMaPhong);
                }

                var list = await query.OrderByDescending(b => b.NgayBaoCao).Take(6).ToListAsync();
                if (!list.Any())
                {
                    return Json(new
                    {
                        response = "📭 Hiện bạn chưa có báo cáo sự cố hay đơn yêu cầu nào trên hệ thống. Bạn có muốn tôi tạo giúp một yêu cầu mới không?",
                        source = "AI Action Agent (Tra cứu tiến độ)",
                        actionCard = new
                        {
                            cardType = "ACTION_HUB",
                            currentMaSV = currentSv?.MaSV ?? "",
                            currentHoTen = currentSv?.HoTen ?? "",
                            currentMaPhong = activeMaPhong
                        }
                    });
                }

                var sb = new StringBuilder($"📋 **Danh sách {list.Count} phiếu báo cáo / yêu cầu gần nhất:**\n");
                foreach (var item in list)
                {
                    string icon = item.TrangThai == "Đã xử lý" ? "✅" : (item.TrangThai == "Đang xử lý" ? "⏳" : "🕒");
                    sb.AppendLine($"• {icon} **{item.MaSuCo}** (P.{item.MaPhong}): {item.TenSuCo} — *{item.TrangThai}* ({item.NgayBaoCao:dd/MM HH:mm})");
                }

                return Json(new
                {
                    response = sb.ToString(),
                    source = "AI Action Agent (Tra cứu tiến độ)"
                });
            }

            return null;
        }

        private List<string> ExtractRoomsFromMessage(string rawMsg, List<Phong> allRooms)
        {
            var result = new List<(int Index, string MaPhong)>();
            string upperMsg = rawMsg.ToUpper();

            foreach (var room in allRooms)
            {
                string code = room.MaPhong.ToUpper();
                string pattern = $@"(?<![A-Z0-9])(P\.?\s*)?{Regex.Escape(code)}(?![A-Z0-9])";
                var m = Regex.Match(upperMsg, pattern);
                if (m.Success)
                {
                    result.Add((m.Index, room.MaPhong));
                }
            }

            return result.OrderBy(x => x.Index).Select(x => x.MaPhong).Distinct().ToList();
        }

        private (bool HasSpecificIssue, string Title, string Urgency) ExtractIncidentDetails(string rawMsg, string msgLower)
        {
            string urgency = "Bình thường";
            if (msgLower.Contains("khẩn cấp") || msgLower.Contains("chập điện") || msgLower.Contains("cháy nổ") || msgLower.Contains("vỡ ống") || msgLower.Contains("nguy hiểm"))
                urgency = "Khẩn cấp";
            else if (msgLower.Contains("nghiêm trọng") || msgLower.Contains("mất điện") || msgLower.Contains("mất nước") || msgLower.Contains("ngập") || msgLower.Contains("tràn nước"))
                urgency = "Nghiêm trọng";

            var issuePatterns = new (string[] Keywords, string Title)[]
            {
                (new[] { "bóng đèn", "đèn điện", "cháy đèn", "hỏng đèn", "đèn nhấp nháy", "đèn tuýp" }, "Hỏng bóng đèn chiếu sáng"),
                (new[] { "vòi nước", "vòi sen", "rò rỉ nước", "rỉ nước", "ống nước", "mất nước", "vỡ ống" }, "Sự cố hệ thống cấp nước / vòi nước"),
                (new[] { "tắc cống", "tắc bồn cầu", "bồn cầu", "thoát nước", "nhà vệ sinh" }, "Tắc nghẽn thoát nước / nhà vệ sinh"),
                (new[] { "điều hòa", "máy lạnh" }, "Sự cố điều hòa nhiệt độ"),
                (new[] { "quạt trần", "quạt điện", "hỏng quạt", "quạt không quay" }, "Hỏng quạt điện trong phòng"),
                (new[] { "ổ cắm", "công tắc", "chập điện", "mất điện", "aptomat", "cầu dao" }, "Sự cố hệ thống điện / ổ cắm"),
                (new[] { "bình nóng lạnh", "nóng lạnh" }, "Sự cố bình nóng lạnh"),
                (new[] { "khóa cửa", "cửa phòng", "cửa sổ", "bản lề", "kẹt cửa", "hỏng khóa" }, "Hỏng khóa cửa / cửa phòng"),
                (new[] { "giường", "tủ đồ", "bàn học", "ghế" }, "Hư hỏng nội thất (giường/tủ/bàn ghế)"),
                (new[] { "wifi", "mạng", "internet" }, "Sự cố kết nối mạng / Wifi")
            };

            foreach (var item in issuePatterns)
            {
                if (item.Keywords.Any(k => msgLower.Contains(k)))
                {
                    return (true, item.Title, urgency);
                }
            }

            // Kiểm tra mẫu câu "báo hỏng [nội dung]" hoặc "bị hỏng [nội dung]"
            var customMatch = Regex.Match(rawMsg, @"(?:báo hỏng|bị hỏng|sửa giúp|sửa hộ)\s+([^,\.]+)", RegexOptions.IgnoreCase);
            if (customMatch.Success)
            {
                string extracted = customMatch.Groups[1].Value.Trim();
                // Loại bỏ các từ chỉ người như "giúp tôi", "hộ tôi", "cho tôi"
                extracted = Regex.Replace(extracted, @"\b(giúp tôi|hộ tôi|giúp mình|hộ mình|cho tôi|cho mình|không|nhé|ạ)\b", "", RegexOptions.IgnoreCase).Trim();
                if (extracted.Length >= 3 && !extracted.Contains("cơ sở vật chất", StringComparison.OrdinalIgnoreCase))
                {
                    return (true, "Sự cố: " + char.ToUpper(extracted[0]) + extracted.Substring(1), urgency);
                }
            }

            return (false, "", urgency);
        }

        private string NormalizeRoomCode(string input)
        {
            string s = (input ?? "").Trim().ToUpper();
            if (s.StartsWith("PHÒNG ")) s = s.Substring(6).Trim();
            if (s.StartsWith("P.")) s = s.Substring(2).Trim();
            else if (s.StartsWith("P ")) s = s.Substring(2).Trim();
            return s;
        }

        private string? CleanAiOutput(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            // Loại bỏ các placeholder markdown link giả định do LLM sinh ra như: [Báo cáo sự cố](link đến mục...)
            string cleaned = Regex.Replace(text, @"\[([^\]]+)\]\((?:link[^)]*|#[^)]*|http://example[^)]*)\)", "'$1'", RegexOptions.IgnoreCase);
            return cleaned;
        }

        private string ExtractJsonString(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "{}";
            var match = Regex.Match(input, @"\{.*\}", RegexOptions.Singleline);
            return match.Success ? match.Value : input;
        }

        private string GetFactsByIntent(string intent)
        {
            try
            {
                string jsonPath = System.IO.Path.Combine(_env.WebRootPath, "data", "knowledge_base.json");
                if (!System.IO.File.Exists(jsonPath)) return "Chưa có dữ liệu.";

                var jsonContent = System.IO.File.ReadAllText(jsonPath);
                using var doc = JsonDocument.Parse(jsonContent);

                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    if (element.GetProperty("intent").GetString() == intent)
                    {
                        var factsList = element.GetProperty("facts").EnumerateArray();
                        return "• " + string.Join("\n• ", factsList.Select(f => f.GetString()));
                    }
                }
            }
            catch { }

            return "Xin lỗi, mình chưa tìm thấy thông tin phù hợp. Bạn có thể hỏi về: 'giá phòng', 'phòng trống', 'giờ giới nghiêm', 'báo sự cố', 'điện nước', 'gửi xe', 'trả phòng', 'hợp đồng', 'kỷ luật', 'thống kê' hoặc nhờ mình thực hiện trực tiếp các thao tác nhé!";
        }

        private async Task<string?> CallOllamaOrNullAsync(string prompt)
        {
            string baseUrl = _configuration["Ollama:BaseUrl"] ?? "http://localhost:11434/api/chat";
            string modelName = _configuration["Ollama:Model"] ?? "llama3";

            var payload = new
            {
                model = modelName,
                messages = new[]
                {
                    new { role = "system", content = AIPrompts.ChatbotSystemPrompt },
                    new { role = "user", content = prompt }
                },
                stream = false
            };

            try
            {
                var httpClient = _httpClientFactory.CreateClient("Ollama");
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var res = await httpClient.PostAsync(baseUrl, content);
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    return doc.RootElement.GetProperty("message").GetProperty("content").GetString();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Ollama chưa khởi chạy tại localhost:11434 ({Msg}) -> Sử dụng Fallback nội bộ.", ex.Message);
            }

            return null;
        }
    }

    public class ChatRequest
    {
        public string? Message { get; set; }
    }

    public class ChatActionExecuteRequest
    {
        public string? ActionType { get; set; }
        public string? MaSV { get; set; }
        public string? MaPhong { get; set; }
        public string? TenSuCo { get; set; }
        public string? MucDoKhanCap { get; set; }
        public string? MoTa { get; set; }
        public string? LoaiYeuCau { get; set; }
        public string? PhongChuyenDen { get; set; }
        public string? LyDo { get; set; }
        public string? MaHoaDon { get; set; }
        public string? PhuongThuc { get; set; }
    }
}
