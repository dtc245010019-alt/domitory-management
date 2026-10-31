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
                bool sessionIsAdmin = HttpContext.Session.GetString("Admin_DangNhap") == "true";

                // BẢO MẬT: Bắt buộc đăng nhập trước khi AI được phép ghi dữ liệu vào CSDL
                if (!sessionIsAdmin && string.IsNullOrWhiteSpace(sessionMaSV))
                    return Json(new { success = false, message = "🔒 Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại để thực hiện thao tác." });

                bool isStudentRole = req.Role == "SinhVien" || (!string.IsNullOrWhiteSpace(sessionMaSV) && req.Role != "Admin");
                // BẢO MẬT: Quyền Admin chỉ lấy từ Session phía máy chủ, không tin tham số "role" do client gửi lên
                bool isAdmin = !isStudentRole && sessionIsAdmin;
                // Sinh viên đang đăng nhập (không phải Admin) -> chỉ được thao tác trên hồ sơ của chính mình
                bool isLoggedInStudent = !isAdmin && !string.IsNullOrWhiteSpace(sessionMaSV);

                // Xác định Sinh viên và Phòng thực hiện thao tác
                SinhVien? sv = null;
                if (isLoggedInStudent)
                {
                    // BẢO MẬT: Bỏ qua req.MaSV do client gửi lên, khóa cứng theo MaSV trong Session
                    sv = await _context.SinhViens.FirstOrDefaultAsync(s => s.MaSV == sessionMaSV);
                }
                else if (!string.IsNullOrWhiteSpace(req.MaSV))
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

                // Với yêu cầu phòng của sinh viên: phòng hiện tại luôn là phòng thực tế trong hồ sơ
                if (isLoggedInStudent && req.ActionType == "GUI_YEU_CAU_PHONG" && !string.IsNullOrWhiteSpace(sv?.MaPhong))
                    maPhong = sv.MaPhong;

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

                    string maSC = $"SC_{DateTime.Now:yyMMddHHmm}_{Random.Shared.Next(1000, 9999)}";
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

                    // Xử lý tự động GIA HẠN HỢP ĐỒNG trực tiếp trên CSDL
                    if (loaiYeuCau.Contains("gia hạn", StringComparison.OrdinalIgnoreCase))
                    {
                        var hd = await _context.HopDongs.FirstOrDefaultAsync(h => h.MaSV == sv.MaSV && h.TrangThai == "Còn hiệu lực")
                                 ?? await _context.HopDongs.FirstOrDefaultAsync(h => h.MaSV == sv.MaSV);

                        DateTime oldEnd = hd?.NgayKetThuc ?? DateTime.Now;
                        DateTime baseDate = oldEnd < DateTime.Now ? DateTime.Now : oldEnd;
                        DateTime newEnd = baseDate.AddMonths(6);

                        if (hd != null)
                        {
                            hd.NgayKetThuc = newEnd;
                            hd.TrangThai = "Còn hiệu lực";
                            hd.DieuKhoan = $"Đã gia hạn thêm 6 tháng qua Trợ lý AI ({DateTime.Now:dd/MM/yyyy})";
                        }
                        else
                        {
                            hd = new HopDong
                            {
                                MaHopDong = "HD_" + DateTime.Now.ToString("yyMMddHHmm"),
                                MaSV = sv.MaSV,
                                MaPhong = maPhong,
                                NgayBatDau = DateTime.Now,
                                NgayKetThuc = newEnd,
                                TienDatCoc = 1000000m,
                                TrangThai = "Còn hiệu lực",
                                DieuKhoan = "Gia hạn tự động qua Trợ lý AI"
                            };
                            _context.HopDongs.Add(hd);
                        }

                        await _context.SaveChangesAsync();

                        return Json(new
                        {
                            success = true,
                            message = $"✅ Đã tự động gia hạn hợp đồng {hd.MaHopDong} thành công đến ngày {newEnd:dd/MM/yyyy}!",
                            receipt = new
                            {
                                maPhieu = hd.MaHopDong,
                                loaiPhieu = "Gia hạn Hợp đồng & Phòng ở (+6 tháng)",
                                maPhong = "P." + maPhong,
                                nguoiGui = $"{sv.MaSV} - {sv.HoTen}",
                                noiDung = $"Gia hạn từ {oldEnd:dd/MM/yyyy} → {newEnd:dd/MM/yyyy}",
                                chiTiet = $"Lý do: {lyDo}",
                                trangThai = "Đã gia hạn (Còn hiệu lực)",
                                thoiGian = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
                            }
                        });
                    }

                    string phongMongMuon;
                    string? phongCuCanDongBo = null;
                    string? phongMoiCanDongBo = null;
                    if (loaiYeuCau.Contains("chuyển phòng", StringComparison.OrdinalIgnoreCase))
                    {
                        string phongDen = NormalizeRoomCode(req.PhongChuyenDen ?? "");
                        if (string.IsNullOrWhiteSpace(phongDen))
                            return Json(new { success = false, message = "Vui lòng chọn hoặc nhập mã phòng muốn chuyển đến!" });

                        if (phongDen.Equals(maPhong, StringComparison.OrdinalIgnoreCase))
                            return Json(new { success = false, message = "Mã phòng muốn chuyển đến phải khác phòng hiện tại!" });

                        // Kiểm tra phòng đích tồn tại, không bảo trì và còn giường trống (Luật đồng bộ sĩ số phòng)
                        var (phongDich, loiPhongDich) = await KiemTraPhongDichAsync(phongDen, sv.MaSV);
                        if (phongDich == null)
                            return Json(new { success = false, message = loiPhongDich });
                        phongDen = phongDich.MaPhong;

                        phongMongMuon = $"Từ phòng {maPhong} -> Chuyển sang phòng {phongDen}";

                        // Cập nhật phòng mới cho Sinh viên và Hợp đồng
                        phongCuCanDongBo = sv.MaPhong;
                        phongMoiCanDongBo = phongDen;
                        sv.MaPhong = phongDen;
                        var activeHd = await _context.HopDongs.FirstOrDefaultAsync(h => h.MaSV == sv.MaSV && h.TrangThai == "Còn hiệu lực");
                        if (activeHd != null)
                        {
                            activeHd.MaPhong = phongDen;
                        }
                    }
                    else
                    {
                        phongMongMuon = $"Phòng hiện tại: {maPhong}";
                    }

                    string maYC = $"YC_{DateTime.Now:yyMMddHHmm}_{Random.Shared.Next(1000, 9999)}";
                    var yeuCau = new BaoCaoSuCo
                    {
                        MaSuCo = maYC,
                        TenSuCo = $"[{loaiYeuCau}] Nguyện vọng: {phongMongMuon}",
                        MoTa = lyDo,
                        MucDoKhanCap = "Bình thường",
                        HinhAnh = "",
                        MaSV = sv.MaSV,
                        MaPhong = sv.MaPhong ?? maPhong,
                        TrangThai = loaiYeuCau.Contains("chuyển phòng", StringComparison.OrdinalIgnoreCase) ? "Đã xử lý" : "Chờ xử lý",
                        NgayBaoCao = DateTime.Now
                    };

                    _context.BaoCaoSuCos.Add(yeuCau);
                    await _context.SaveChangesAsync();

                    // Tự động đồng bộ trạng thái "Còn chỗ" / "Đã đầy" cho phòng cũ và phòng mới
                    await DongBoTrangThaiPhongAsync(phongCuCanDongBo);
                    await DongBoTrangThaiPhongAsync(phongMoiCanDongBo);

                    return Json(new
                    {
                        success = true,
                        message = $"✅ Đã thực hiện '{loaiYeuCau}' thành công! Mã phiếu: {maYC}",
                        receipt = new
                        {
                            maPhieu = maYC,
                            loaiPhieu = loaiYeuCau,
                            maPhong = "P." + (sv.MaPhong ?? maPhong),
                            nguoiGui = $"{sv.MaSV} - {sv.HoTen}",
                            noiDung = phongMongMuon,
                            chiTiet = $"Lý do: {lyDo}",
                            trangThai = loaiYeuCau.Contains("chuyển phòng", StringComparison.OrdinalIgnoreCase) ? "Đã chuyển phòng thành công" : "Chờ Ban Quản lý duyệt",
                            thoiGian = yeuCau.NgayBaoCao.ToString("dd/MM/yyyy HH:mm")
                        }
                    });
                }

                // 3. XÁC NHẬN THANH TOÁN HÓA ĐƠN (CHỈ DÀNH CHO ADMIN XÁC NHẬN)
                if (req.ActionType == "THANH_TOAN_HOA_DON")
                {
                    if (!isAdmin)
                    {
                        return Json(new
                        {
                            success = false,
                            message = "⛔ Vì lý do an toàn tài chính, AI không được phép tự động thanh toán hóa đơn. Vui lòng tự thực hiện thanh toán tại trang 'Hóa đơn của tôi' (yêu cầu mật khẩu xác thực) hoặc đến gặp trực tiếp Ban Quản lý để được Admin xác nhận thu tiền."
                        });
                    }

                    if (string.IsNullOrWhiteSpace(req.MaHoaDon))
                        return Json(new { success = false, message = "Vui lòng chọn mã hóa đơn cần xác nhận thanh toán." });

                    var hd = await _context.HoaDons
                        .Include(h => h.SinhVien)
                        .FirstOrDefaultAsync(h => h.MaHoaDon == req.MaHoaDon.Trim());

                    if (hd == null)
                        return Json(new { success = false, message = $"Không tìm thấy hóa đơn mã {req.MaHoaDon}." });

                    if (hd.TrangThai == "Đã thanh toán")
                        return Json(new { success = false, message = $"Hóa đơn {hd.MaHoaDon} đã được thanh toán trước đó." });

                    string phuongThuc = string.IsNullOrWhiteSpace(req.PhuongThuc) ? "Tiền mặt / Thu trực tiếp (Admin xác nhận)" : req.PhuongThuc.Trim();
                    hd.TrangThai = "Đã thanh toán";
                    hd.HinhThucThanhToan = phuongThuc;
                    await _context.SaveChangesAsync();

                    return Json(new
                    {
                        success = true,
                        message = $"✅ Ban Quản lý (Admin) đã xác nhận thu tiền và gạch nợ thành công hóa đơn {hd.MaHoaDon}!",
                        receipt = new
                        {
                            maPhieu = hd.MaHoaDon,
                            loaiPhieu = $"Xác nhận thanh toán {hd.LoaiHoaDon}",
                            maPhong = "P." + hd.MaPhong,
                            nguoiGui = "Ban Quản lý KTX (Admin)",
                            noiDung = $"Số tiền: {hd.TongTien:N0} VNĐ",
                            chiTiet = $"Phương thức: {phuongThuc}",
                            trangThai = "Đã thanh toán (Admin xác nhận)",
                            thoiGian = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
                        }
                    });
                }

                // 4. DUYỆT / CẬP NHẬT TRẠNG THÁI SỰ CỐ (DÀNH CHO ADMIN)
                if (req.ActionType == "CAP_NHAT_TRANG_THAI_SU_CO")
                {
                    // BẢO MẬT: Chỉ Ban Quản lý (Admin) mới được duyệt sự cố (đồng bộ với [AdminAuthorize(OnlyAdmin = true)] của QuanLySuCoAdmin)
                    bool isKeToan = string.Equals(HttpContext.Session.GetString("VaiTro"), "KeToan", StringComparison.OrdinalIgnoreCase);
                    if (!isAdmin || isKeToan)
                        return Json(new { success = false, message = "⛔ Chỉ Ban Quản lý KTX (Admin) mới có quyền duyệt / cập nhật trạng thái sự cố." });

                    if (string.IsNullOrWhiteSpace(req.MaSuCo))
                        return Json(new { success = false, message = "Vui lòng cung cấp mã sự cố cần cập nhật." });

                    var sc = await _context.BaoCaoSuCos.FirstOrDefaultAsync(s => s.MaSuCo == req.MaSuCo.Trim());
                    if (sc == null)
                        return Json(new { success = false, message = $"Không tìm thấy sự cố mã {req.MaSuCo}." });

                    string trangThaiMoi = string.IsNullOrWhiteSpace(req.TrangThaiMoi) ? "Đã xử lý" : req.TrangThaiMoi.Trim();
                    sc.TrangThai = trangThaiMoi;
                    await _context.SaveChangesAsync();

                    return Json(new
                    {
                        success = true,
                        message = $"✅ Đã cập nhật trạng thái sự cố {sc.MaSuCo} thành '{trangThaiMoi}' thành công!",
                        receipt = new
                        {
                            maPhieu = sc.MaSuCo,
                            loaiPhieu = "Cập nhật tiến độ sự cố",
                            maPhong = "P." + sc.MaPhong,
                            nguoiGui = "Ban Quản lý KTX (Admin)",
                            noiDung = sc.TenSuCo,
                            chiTiet = $"Trạng thái mới: {trangThaiMoi}",
                            trangThai = trangThaiMoi,
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

        private async Task<(SinhVien? Sv, string MaPhong, List<Phong> AllRooms, List<string> MentionedRooms)> ResolveStudentContextAsync(string rawMsg)
        {
            var allRooms = await _context.Phongs.AsNoTracking().OrderBy(p => p.MaPhong).ToListAsync();
            var mentionedRooms = ExtractRoomsFromMessage(rawMsg, allRooms);

            string sessionMaSV = HttpContext.Session.GetString("MaSV_DangNhap") ?? "";
            SinhVien? currentSv = null;

            // 0. BẢO MẬT: Sinh viên đang đăng nhập -> LUÔN khóa cứng vào hồ sơ của chính mình.
            //    Không cho phép gõ mã SV khác (VD: "DTC005") để xem/thao tác trên hồ sơ người khác.
            if (!string.IsNullOrWhiteSpace(sessionMaSV))
            {
                currentSv = await _context.SinhViens.FirstOrDefaultAsync(s => s.MaSV == sessionMaSV);
                if (currentSv != null)
                {
                    string phongDangXet = mentionedRooms.FirstOrDefault() ?? currentSv.MaPhong ?? "101";
                    return (currentSv, phongDangXet, allRooms, mentionedRooms);
                }
            }

            // 1. Nếu câu nhập có nhắc trực tiếp mã SV (VD: DTC001)
            var matchSv = Regex.Match(rawMsg.ToUpper(), @"DTC\d+");
            if (matchSv.Success)
            {
                currentSv = await _context.SinhViens.FirstOrDefaultAsync(s => s.MaSV == matchSv.Value);
            }

            // 2. Nếu sinh viên đang đăng nhập trên hệ thống
            if (currentSv == null && !string.IsNullOrWhiteSpace(sessionMaSV))
            {
                currentSv = await _context.SinhViens.FirstOrDefaultAsync(s => s.MaSV == sessionMaSV);
            }

            // 3. Mã phòng đang xét
            string activeMaPhong = mentionedRooms.FirstOrDefault() ?? currentSv?.MaPhong ?? "";

            // 4. Nếu đang mở ở giao diện Admin/Khách chưa có MaSV_DangNhap -> Giữ cố định 1 SV đại diện trong phiên để đồng nhất giữa Hỏi đáp & Làm hộ
            if (currentSv == null)
            {
                string demoMaSV = HttpContext.Session.GetString("Chatbot_DemoMaSV") ?? "";
                if (!string.IsNullOrWhiteSpace(demoMaSV))
                {
                    currentSv = await _context.SinhViens.FirstOrDefaultAsync(s => s.MaSV == demoMaSV);
                }

                if (currentSv == null)
                {
                    var svWithUnpaid = await _context.HoaDons
                        .Where(h => h.TrangThai != "Đã thanh toán")
                        .Select(h => h.MaSV)
                        .FirstOrDefaultAsync();

                    if (!string.IsNullOrWhiteSpace(svWithUnpaid))
                    {
                        currentSv = await _context.SinhViens.FirstOrDefaultAsync(s => s.MaSV == svWithUnpaid);
                    }

                    currentSv ??= await _context.SinhViens.FirstOrDefaultAsync(s => s.TinhTrangLuuTru == "Đang ở" && s.MaPhong != null)
                                  ?? await _context.SinhViens.FirstOrDefaultAsync();

                    if (currentSv != null)
                    {
                        HttpContext.Session.SetString("Chatbot_DemoMaSV", currentSv.MaSV);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(activeMaPhong))
            {
                activeMaPhong = currentSv?.MaPhong ?? "101";
            }

            return (currentSv, activeMaPhong, allRooms, mentionedRooms);
        }

        private async Task<IActionResult> AskInternal(ChatRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
                return Json(new { response = "Bạn cần hỗ trợ thông tin gì hoặc thực hiện tác vụ nào về KTX?" });

            string rawMsg = request.Message.Trim();
            if (!SafetyGuardrails.ValidateInput(rawMsg, out var sanitizedMsg, out var validationError))
            {
                return Json(new { response = validationError, source = "Rào chắn an toàn (Safety Guardrails)" });
            }
            rawMsg = sanitizedMsg;
            string msgLower = rawMsg.ToLower();
            string mode = (request.Mode ?? "").Trim().ToUpper(); // "ACTION" hoặc "QA"
            string sessionMaSV = HttpContext.Session.GetString("MaSV_DangNhap") ?? "";
            bool sessionIsAdmin = HttpContext.Session.GetString("Admin_DangNhap") == "true";

            // Phân định vai trò chuẩn xác:
            // 1. Nếu từ Cổng sinh viên (_StudentLayout) -> request.Role = "SinhVien" -> CHẮC CHẮN LÀ SINH VIÊN
            // 2. Nếu có Session sinh viên (MaSV_DangNhap) và request không chỉ định "Admin" -> SINH VIÊN
            // 3. Chỉ coi là Admin khi: request.Role = "Admin" HOẶC (sessionIsAdmin && không có session MaSV_DangNhap && request.Role != "SinhVien")
            // BẢO MẬT: Bắt buộc đăng nhập khi yêu cầu thực hiện hành động ghi CSDL
            if (!sessionIsAdmin && string.IsNullOrWhiteSpace(sessionMaSV))
            {
                if (mode == "ACTION" || !string.IsNullOrEmpty(DetectActionIntent(msgLower, forceActionMode: false)))
                {
                    return Json(new { response = "🔒 Thao tác này yêu cầu xác thực danh tính. Vui lòng đăng nhập Cổng sinh viên hoặc Quản trị viên để thực hiện.", source = "Hệ thống bảo mật KTX" });
                }
            }

            bool isStudentRole = string.Equals(request.Role, "SinhVien", StringComparison.OrdinalIgnoreCase)
                                 || (!string.Equals(request.Role, "Admin", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(sessionMaSV));

            // BẢO MẬT: Quyền Admin CHỈ được xác định từ Session phía máy chủ, không tin tham số "role" do client gửi lên
            bool isAdmin = !isStudentRole && sessionIsAdmin;

            // 0. TỪ CHỐI CÁC CÂU HỎI NGOÀI LỀ KHÔNG LIÊN QUAN ĐẾN KÝ TÚC XÁ NGAY TỪ ĐẦU (Áp dụng cho cả Admin và Sinh viên)
            if (IsOutOfDomain(msgLower))
            {
                string oodResponse = isAdmin
                    ? "💡 **Trợ lý Điều hành & Quản trị Ký túc xá (Admin Copilot):**\n" +
                      "Xin lỗi Admin, mình chưa tìm thấy thông tin phù hợp trong phạm vi Ký túc xá. Hệ thống Trợ lý Điều hành **chỉ hỗ trợ các thông tin và nghiệp vụ quản trị Ký túc xá** " +
                      "(thống kê sinh viên, quản lý phòng ở, cơ sở vật chất, hóa đơn điện nước, xử lý sự cố, hợp đồng lưu trú và kỷ luật).\n" +
                      "Câu hỏi này nằm ngoài phạm vi quản lý KTX nên hệ thống xin phép từ chối phản hồi."
                    : "💡 **Trợ lý Ký túc xá (QLKTX):**\n" +
                      "Xin lỗi bạn, mình chưa tìm thấy thông tin phù hợp trong phạm vi Ký túc xá. Mình là Trợ lý AI Ký túc xá và **chỉ trả lời các nội dung liên quan đến Ký túc xá** " +
                      "(nội quy, giá phòng, dịch vụ điện nước, gửi xe, tiếp khách, báo hỏng thiết bị, thủ tục chuyển phòng / trả phòng).\n" +
                      "Câu hỏi của bạn không thuộc phạm vi KTX nên mình xin phép không trả lời. Bạn vui lòng đặt câu hỏi về Ký túc xá nhé!";

                return Json(new { response = oodResponse, source = "Phạm vi phục vụ Trợ lý KTX" });
            }

            IActionResult result = isAdmin
                ? await HandleAdminFlowAsync(rawMsg, msgLower, mode)
                : await HandleStudentFlowAsync(rawMsg, msgLower, mode);

            // Câu hỏi vừa tra dữ liệu vừa hỏi nội quy: bổ sung phần nội quy mà nhánh dữ liệu chưa trả lời
            return AppendRelatedPolicy(result, rawMsg, msgLower);
        }

        // =========================================================================
        // LUỒNG XỬ LÝ DÀNH RIÊNG CHO QUẢN TRỊ VIÊN (ADMIN CHATBOT COPILOT)
        // =========================================================================
        private async Task<IActionResult> HandleAdminFlowAsync(string rawMsg, string msgLower, string mode)
        {
            var allRooms = await _context.Phongs.AsNoTracking().ToListAsync();
            var mentionedRooms = ExtractRoomsFromMessage(rawMsg, allRooms);
            var matchSv = Regex.Match(rawMsg.ToUpper(), @"DTC\d+");
            bool policyOnly = IsPolicyOnlyQuestion(msgLower, mentionedRooms.Any() || matchSv.Success);

            // 1. Chặn và nhắc nhở nếu Admin đặt các câu hỏi thuộc ngữ cảnh cá nhân của sinh viên
            bool isStudentPersonalOnly = (msgLower.Contains("của tôi") || msgLower.Contains("của mình") ||
                                          msgLower.Contains("phòng tôi") || msgLower.Contains("cho tôi") ||
                                          msgLower.Contains("bạn cùng phòng") || msgLower.Contains("ở cùng tôi"))
                                         && !mentionedRooms.Any() && !matchSv.Success && !policyOnly;

            if (isStudentPersonalOnly)
            {
                string adminHint = "⚠️ **Thông báo quyền hạn Quản trị viên (Admin):**\n" +
                                   "Bạn đang đăng nhập bằng tài khoản **Ban Quản lý KTX**. Các câu hỏi cá nhân của sinh viên (như *'hóa đơn của tôi'*, *'phòng tôi'*, *'bạn cùng phòng'*) không áp dụng cho vai trò này.\n\n" +
                                   "👉 **Với tư cách Admin, bạn có thể thực hiện:**\n" +
                                   "• **Tra cứu theo phòng:** Gõ *\"Phòng 101 có những ai?\"*, *\"Hóa đơn phòng 102\"*, *\"Thiết bị phòng 103\"*\n" +
                                   "• **Tra cứu theo sinh viên:** Gõ *\"Sinh viên DTC001 nợ bao nhiêu?\"*, *\"Hồ sơ sinh viên DTC002\"*\n" +
                                   "• **Báo cáo điều hành:** Gõ *\"Báo cáo thống kê KTX\"*, *\"Danh sách phòng còn trống\"*, *\"Sự cố chờ xử lý\"*, *\"Hợp đồng sắp hết hạn\"*";
                return Json(new { response = adminHint, source = "Hệ thống Quản trị KTX (Admin)" });
            }

            // 2. XỬ LÝ CHẾ ĐỘ ACTION (THAO TÁC QUẢN TRỊ / LÀM HỘ ADMIN)
            if (mode == "ACTION" || msgLower.Contains("duyệt sự cố") || msgLower.Contains("xử lý sự cố") ||
                msgLower.Contains("nhắc nợ") || msgLower.Contains("gợi ý xếp phòng") || msgLower.Contains("xuất báo cáo"))
            {
                // A. Duyệt / Cập nhật sự cố
                if (msgLower.Contains("sự cố") || msgLower.Contains("bảo trì") || msgLower.Contains("hỏng") || msgLower.Contains("duyệt"))
                {
                    var pendingIncidents = await _context.BaoCaoSuCos
                        .Where(s => s.TrangThai != "Đã xử lý")
                        .OrderByDescending(s => s.NgayBaoCao)
                        .Take(8)
                        .ToListAsync();

                    if (!pendingIncidents.Any())
                    {
                        return Json(new
                        {
                            response = "✅ **Hệ thống ghi nhận:** Hiện tại không còn sự cố kỹ thuật nào đang chờ xử lý trên toàn hệ thống KTX.",
                            source = "Quản trị KTX - Phê duyệt sự cố"
                        });
                    }

                    return Json(new
                    {
                        response = $"🚨 **Ban Quản lý có {pendingIncidents.Count} sự cố kỹ thuật đang chờ giải quyết.** Bạn có thể bấm nút duyệt ngay trên thẻ điều khiển dưới đây:",
                        source = "Quản trị KTX - Phê duyệt sự cố",
                        actionCard = new
                        {
                            cardType = "ADMIN_SU_CO_LIST",
                            incidents = pendingIncidents.Select(x => new
                            {
                                maSuCo = x.MaSuCo,
                                tenSuCo = x.TenSuCo,
                                maPhong = x.MaPhong,
                                mucDoKhanCap = x.MucDoKhanCap,
                                ngayBaoCao = x.NgayBaoCao.ToString("dd/MM/yyyy HH:mm"),
                                trangThai = x.TrangThai,
                                moTa = x.MoTa
                            }).ToList()
                        }
                    });
                }

                // B. Đôn đốc nhắc nợ hóa đơn
                if (msgLower.Contains("nhắc nợ") || msgLower.Contains("thông báo đóng tiền") || msgLower.Contains("chưa nộp") || msgLower.Contains("công nợ"))
                {
                    var unpaidInvoices = await _context.HoaDons
                        .Include(h => h.SinhVien)
                        .Where(h => h.TrangThai != "Đã thanh toán")
                        .OrderByDescending(h => h.TongTien)
                        .ToListAsync();

                    decimal totalDebt = unpaidInvoices.Sum(h => h.TongTien);
                    var debtors = unpaidInvoices.Take(6).Select(h => $"• P.{h.MaPhong} - {h.SinhVien?.HoTen ?? h.MaSV} ({h.LoaiHoaDon}): **{h.TongTien:N0} VNĐ**").ToList();

                    string reminderMsg = $"🧾 **BẢNG RÀ SOÁT CÔNG NỢ & MẪU THÔNG BÁO NHẮC NỢ:**\n\n" +
                                         $"• **Tổng công nợ tồn đọng:** **{totalDebt:N0} VNĐ** ({unpaidInvoices.Count} hóa đơn chưa thu).\n" +
                                         $"• **Danh sách các phòng nợ tiêu biểu:**\n{string.Join("\n", debtors)}\n\n" +
                                         $"📢 **Mẫu thông báo phát thanh / gửi sinh viên:**\n" +
                                         $"> *\"[THÔNG BÁO BQL KTX] Đề nghị sinh viên tại các phòng còn nợ hóa đơn tiền phòng/điện nước khẩn trương thanh toán qua Cổng Student Portal hoặc Văn phòng BQL trước ngày 30 hàng tháng.\"*";

                    return Json(new
                    {
                        response = reminderMsg,
                        source = "Quản trị KTX - Rà soát công nợ"
                    });
                }

                // C. Gợi ý xếp phòng
                if (msgLower.Contains("xếp phòng") || msgLower.Contains("gợi ý phòng") || msgLower.Contains("tìm phòng"))
                {
                    var availableRooms = await _context.Phongs
                        .Include(p => p.SinhViens)
                        .Where(p => p.TinhTrang == "Còn chỗ")
                        .ToListAsync();

                    var sbRec = new StringBuilder();
                    sbRec.AppendLine("🚪 **Gợi ý các phòng còn chỗ trống phù hợp để tiếp nhận sinh viên:**\n");
                    foreach (var r in availableRooms.Take(6))
                    {
                        int emptyBeds = r.SoLuongGiuong - (r.SinhViens?.Count(s => s.TinhTrangLuuTru == "Đang ở") ?? 0);
                        sbRec.AppendLine($"• **Phòng P.{r.MaPhong}** ({r.LoaiPhong}): Còn trống **{emptyBeds}/{r.SoLuongGiuong} giường** — Đơn giá: **{r.GiaPhong:N0} VNĐ/tháng/SV**");
                    }
                    return Json(new
                    {
                        response = sbRec.ToString().Trim(),
                        source = "Quản trị KTX - Tư vấn Xếp phòng"
                    });
                }

                // D. Mặc định Action Hub cho Admin
                return Json(new
                {
                    response = "⚡ **Bảng điều hành tác vụ Quản trị KTX (Admin Action Agent)** đang sẵn sàng hỗ trợ bạn điều hành hệ thống:",
                    source = "Quản trị KTX - Điều hành",
                    actionCard = new
                    {
                        cardType = "ADMIN_ACTION_HUB"
                    }
                });
            }

            // 3. XỬ LÝ CHẾ ĐỘ HỎI ĐÁP QUẢN TRỊ (ADMIN QA MODE)
            string intent = ClassifyQaIntent(msgLower);

            // Câu hỏi thuần về nội quy/biểu phí (VD: "giờ đóng cửa và giá điện nước") -> trả lời từ cơ sở tri thức, không tra số liệu
            if (policyOnly)
                return await AnswerAdminFromKnowledgeAsync(rawMsg, msgLower, intent);

            // A. THỐNG KÊ TỔNG QUAN KTX
            if (intent == "THONG_KE" || msgLower.Contains("tổng quan") || (msgLower.Contains("báo cáo") && intent == "KHAC"))
            {
                var tongSV = await _context.SinhViens.CountAsync(s => s.TinhTrangLuuTru == "Đang ở");
                var svDaRoi = await _context.SinhViens.CountAsync(s => s.TinhTrangLuuTru == "Đã rời đi");
                var tongPhong = await _context.Phongs.CountAsync();
                var phongConCho = await _context.Phongs.CountAsync(p => p.TinhTrang == "Còn chỗ");
                var tongHopDong = await _context.HopDongs.CountAsync(h => h.TrangThai == "Còn hiệu lực");
                var hdChuaThu = await _context.HoaDons.CountAsync(h => h.TrangThai != "Đã thanh toán");
                var tongCongNo = await _context.HoaDons.Where(h => h.TrangThai != "Đã thanh toán").SumAsync(h => h.TongTien);
                var tongDaThu = await _context.HoaDons.Where(h => h.TrangThai == "Đã thanh toán").SumAsync(h => h.TongTien);
                var suCoChoXuLy = await _context.BaoCaoSuCos.CountAsync(b => b.TrangThai != "Đã xử lý" && !b.MaSuCo.StartsWith("YC_") && !b.TenSuCo.StartsWith("["));
                var tongViPham = await _context.ViPhams.CountAsync();

                string statsMsg = $"📊 **BÁO CÁO TỔNG QUAN TÌNH HÌNH QUẢN TRỊ KÝ TÚC XÁ:**\n\n" +
                                  $"👥 **Tình hình sinh viên:**\n" +
                                  $"• Đang lưu trú: **{tongSV} sinh viên** | Đã rời KTX: **{svDaRoi} sinh viên**\n\n" +
                                  $"🏢 **Tình trạng phòng ở & Lấp đầy:**\n" +
                                  $"• Tổng số phòng: **{tongPhong} phòng**\n" +
                                  $"• Phòng còn chỗ trống: **{phongConCho} phòng** | Phòng đã kín/bảo trì: **{tongPhong - phongConCho} phòng**\n\n" +
                                  $"📄 **Hợp đồng & Pháp lý:**\n" +
                                  $"• Hợp đồng đang còn hiệu lực: **{tongHopDong} hợp đồng**\n\n" +
                                  $"💰 **Tài chính & Công nợ:**\n" +
                                  $"• Hóa đơn đã thu: **{tongDaThu:N0} VNĐ**\n" +
                                  $"• Công nợ chưa thanh toán: **{tongCongNo:N0} VNĐ** ({hdChuaThu} hóa đơn tồn đọng)\n\n" +
                                  $"🛠 **Vận hành & Kỷ luật:**\n" +
                                  $"• Sự cố kỹ thuật chưa xử lý xong: **{suCoChoXuLy} sự cố**\n" +
                                  $"• Tổng số biên bản kỷ luật đã lập: **{tongViPham} trường hợp**";

                return Json(new
                {
                    response = statsMsg,
                    source = "Quản trị KTX - Báo cáo Tổng quan",
                    quickAction = new { label = "🚨 Xem và Duyệt sự cố kỹ thuật", prompt = "Duyệt sự cố kỹ thuật đang chờ xử lý", mode = "ACTION" }
                });
            }

            // B. QUẢN LÝ PHÒNG & TRA CỨU PHÒNG
            if (intent == "PHONG")
            {
                var phongs = await _context.Phongs.Include(p => p.SinhViens).AsNoTracking().ToListAsync();

                // Nếu có mã phòng cụ thể (VD: P.101, 102...)
                if (mentionedRooms.Any())
                {
                    string targetRoom = mentionedRooms.First();
                    var pObj = phongs.FirstOrDefault(p => p.MaPhong.Equals(targetRoom, StringComparison.OrdinalIgnoreCase));
                    if (pObj != null)
                    {
                        var residents = pObj.SinhViens?.Where(s => s.TinhTrangLuuTru == "Đang ở").ToList() ?? new List<SinhVien>();
                        int emptyBeds = Math.Max(0, pObj.SoLuongGiuong - residents.Count);

                        var sbRoom = new StringBuilder();
                        sbRoom.AppendLine($"🏢 **Hồ sơ quản lý Phòng P.{pObj.MaPhong}:**");
                        sbRoom.AppendLine($"• Loại phòng: **{pObj.LoaiPhong}** | Đơn giá: **{pObj.GiaPhong:N0} VNĐ/tháng/SV**");
                        sbRoom.AppendLine($"• Sức chứa: **{residents.Count}/{pObj.SoLuongGiuong} sinh viên** (Còn trống: **{emptyBeds} giường**)");
                        sbRoom.AppendLine($"• Tình trạng: **{pObj.TinhTrang}** | Thiết bị: **{pObj.TrangThaiThietBi ?? "Bình thường"}**\n");

                        if (residents.Any())
                        {
                            sbRoom.AppendLine($"👥 **Danh sách sinh viên đang lưu trú ({residents.Count} SV):**");
                            int i = 1;
                            foreach (var s in residents)
                            {
                                sbRoom.AppendLine($"{i}. **{s.HoTen}** — Mã SV: `{s.MaSV}` | Lớp: {s.Lop} | SĐT: {s.SoDienThoai}");
                                i++;
                            }
                        }
                        else
                        {
                            sbRoom.AppendLine("📭 Phòng hiện tại đang trống hoàn toàn.");
                        }

                        return Json(new
                        {
                            response = sbRoom.ToString().Trim(),
                            source = "Quản trị KTX - Tra cứu Phòng"
                        });
                    }
                }

                // Nếu hỏi chung về phòng còn trống
                var vacantRooms = phongs
                    .Select(p => new
                    {
                        p.MaPhong,
                        p.LoaiPhong,
                        p.SoLuongGiuong,
                        Occupied = p.SinhViens?.Count(s => s.TinhTrangLuuTru == "Đang ở") ?? 0,
                        Vacant = p.SoLuongGiuong - (p.SinhViens?.Count(s => s.TinhTrangLuuTru == "Đang ở") ?? 0),
                        p.GiaPhong,
                        p.TinhTrang
                    })
                    .Where(p => p.TinhTrang == "Còn chỗ" && p.Vacant > 0)
                    .OrderByDescending(p => p.Vacant)
                    .ToList();

                var sbEmpty = new StringBuilder();
                sbEmpty.AppendLine($"🚪 **Thống kê các phòng còn giường trống trong KTX ({vacantRooms.Count} phòng):**\n");
                foreach (var r in vacantRooms.Take(8))
                {
                    sbEmpty.AppendLine($"• **Phòng P.{r.MaPhong}** ({r.LoaiPhong}): Trống **{r.Vacant}/{r.SoLuongGiuong} giường** — Giá: **{r.GiaPhong:N0} VNĐ/tháng/SV**");
                }

                return Json(new
                {
                    response = sbEmpty.ToString().Trim(),
                    source = "Quản trị KTX - Phòng trống",
                    quickAction = new { label = "🚪 Nhờ AI gợi ý xếp phòng trống", prompt = "Gợi ý xếp phòng còn chỗ trống", mode = "ACTION" }
                });
            }

            // C. QUẢN LÝ HÓA ĐƠN & CÔNG NỢ TOÀN HỆ THỐNG
            if (intent == "HOADON_DIEN_NUOC")
            {
                var query = _context.HoaDons.Include(h => h.SinhVien).AsNoTracking().AsQueryable();

                if (mentionedRooms.Any())
                {
                    string r = mentionedRooms.First();
                    query = query.Where(h => h.MaPhong == r);
                }
                else if (matchSv.Success)
                {
                    query = query.Where(h => h.MaSV == matchSv.Value);
                }

                var invoices = await query.OrderByDescending(h => h.NgayLap).ToListAsync();
                var unpaid = invoices.Where(h => h.TrangThai != "Đã thanh toán").ToList();

                var sbInv = new StringBuilder();
                if (mentionedRooms.Any() || matchSv.Success)
                {
                    string target = mentionedRooms.Any() ? $"Phòng P.{mentionedRooms.First()}" : $"Sinh viên {matchSv.Value}";
                    sbInv.AppendLine($"🧾 **Danh sách Hóa đơn của {target} ({invoices.Count} hóa đơn):**\n");
                    foreach (var h in invoices)
                    {
                        string stt = h.TrangThai == "Đã thanh toán" ? "✅ Đã thanh toán" : "⚠️ Chưa thanh toán";
                        sbInv.AppendLine($"• **{h.MaHoaDon}** ({h.LoaiHoaDon}): **{h.TongTien:N0} VNĐ** — {stt} ({h.NgayLap:dd/MM/yyyy})");
                    }
                }
                else
                {
                    decimal totalUnpaid = unpaid.Sum(h => h.TongTien);
                    sbInv.AppendLine($"🧾 **BÁO CÁO CÔNG NỢ TOÀN BỘ KÝ TÚC XÁ:**\n");
                    sbInv.AppendLine($"• **Tổng số hóa đơn chưa thu:** **{unpaid.Count}** hóa đơn");
                    sbInv.AppendLine($"• **Tổng số tiền nợ:** **{totalUnpaid:N0} VNĐ**\n");
                    sbInv.AppendLine("📋 **Danh sách các hóa đơn nợ tiêu biểu:**");
                    foreach (var h in unpaid.Take(8))
                    {
                        sbInv.AppendLine($"• **{h.MaHoaDon}** (P.{h.MaPhong} - {h.SinhVien?.HoTen ?? h.MaSV}): **{h.TongTien:N0} VNĐ** ({h.LoaiHoaDon})");
                    }
                }

                return Json(new
                {
                    response = sbInv.ToString().Trim(),
                    source = "Quản trị KTX - Quản lý Hóa đơn & Công nợ",
                    quickAction = new { label = "🧾 Lập mẫu thông báo nhắc nợ", prompt = "Lập thông báo nhắc nợ tiền KTX", mode = "ACTION" }
                });
            }

            // D. QUẢN LÝ HỢP ĐỒNG (HỢP ĐỒNG HẾT HẠN / CÒN HIỆU LỰC)
            if (intent == "HOP_DONG")
            {
                var today = DateTime.Now.Date;
                var activeQuery = _context.HopDongs.Include(h => h.SinhVien).Where(h => h.TrangThai == "Còn hiệu lực");

                // Tách riêng: hợp đồng sắp hết hạn (0..30 ngày tới) và hợp đồng đã quá hạn nhưng chưa thanh lý
                int nearExpireCount = await activeQuery.CountAsync(h => h.NgayKetThuc >= today && h.NgayKetThuc <= today.AddDays(30));
                var nearExpire = await activeQuery
                    .Where(h => h.NgayKetThuc >= today && h.NgayKetThuc <= today.AddDays(30))
                    .OrderBy(h => h.NgayKetThuc)
                    .Take(8)
                    .ToListAsync();
                int overdueCount = await activeQuery.CountAsync(h => h.NgayKetThuc < today);
                var overdue = await activeQuery
                    .Where(h => h.NgayKetThuc < today)
                    .OrderBy(h => h.NgayKetThuc)
                    .Take(8)
                    .ToListAsync();

                int totalActive = await _context.HopDongs.CountAsync(h => h.TrangThai == "Còn hiệu lực");
                int totalExpired = await _context.HopDongs.CountAsync(h => h.TrangThai != "Còn hiệu lực");

                var sbHd = new StringBuilder();
                sbHd.AppendLine($"📄 **BÁO CÁO HỢP ĐỒNG LƯU TRÚ KTX:**\n");
                sbHd.AppendLine($"• Hợp đồng đang ở trạng thái \"Còn hiệu lực\": **{totalActive}** | Đã thanh lý/hết hiệu lực: **{totalExpired}**");
                sbHd.AppendLine($"• Sắp hết hạn trong 30 ngày tới: **{nearExpireCount}** hợp đồng");
                sbHd.AppendLine($"• Đã quá ngày kết thúc nhưng chưa thanh lý/gia hạn: **{overdueCount}** hợp đồng\n");

                if (nearExpire.Any())
                {
                    sbHd.AppendLine($"⏳ **Sắp hết hạn (hiển thị {nearExpire.Count}/{nearExpireCount}):**");
                    foreach (var h in nearExpire)
                    {
                        sbHd.AppendLine($"• **{h.MaHopDong}** — SV: {h.SinhVien?.HoTen ?? h.MaSV} (P.{h.MaPhong}): Hết hạn {h.NgayKetThuc:dd/MM/yyyy} (*còn {(h.NgayKetThuc.Date - today).Days} ngày*)");
                    }
                }
                else
                {
                    sbHd.AppendLine("✅ Không có hợp đồng nào hết hạn trong 30 ngày tới.");
                }

                if (overdue.Any())
                {
                    sbHd.AppendLine($"\n⚠️ **Đã quá hạn, cần gia hạn hoặc thanh lý (hiển thị {overdue.Count}/{overdueCount}):**");
                    foreach (var h in overdue)
                    {
                        sbHd.AppendLine($"• **{h.MaHopDong}** — SV: {h.SinhVien?.HoTen ?? h.MaSV} (P.{h.MaPhong}): Hết hạn {h.NgayKetThuc:dd/MM/yyyy} (*quá hạn {(today - h.NgayKetThuc.Date).Days} ngày*)");
                    }
                }

                return Json(new
                {
                    response = sbHd.ToString().Trim(),
                    source = "Quản trị KTX - Quản lý Hợp đồng"
                });
            }

            // E. QUẢN LÝ SỰ CỐ & TIẾN ĐỘ SỬA CHỮA
            if (intent == "SU_CO")
            {
                // Bảng BaoCaoSuCos chứa cả yêu cầu chuyển/trả/gia hạn phòng (MaSuCo "YC_..." hoặc TenSuCo "[...]") -> loại ra khỏi sự cố kỹ thuật
                var pendingQuery = _context.BaoCaoSuCos
                    .Where(s => s.TrangThai != "Đã xử lý" && !s.MaSuCo.StartsWith("YC_") && !s.TenSuCo.StartsWith("["));
                int totalPending = await pendingQuery.CountAsync();
                var incidents = await pendingQuery
                    .OrderByDescending(s => s.NgayBaoCao)
                    .Take(8)
                    .ToListAsync();

                var sbSc = new StringBuilder();
                sbSc.AppendLine($"🚨 **SỰ CỐ KỸ THUẬT CHƯA XỬ LÝ XONG: {totalPending} sự cố** (Chờ xử lý + Đang xử lý; hiển thị {incidents.Count} sự cố mới nhất):\n");
                if (incidents.Any())
                {
                    foreach (var sc in incidents)
                    {
                        sbSc.AppendLine($"• **{sc.MaSuCo}** (P.{sc.MaPhong}): **{sc.TenSuCo}** — Mức độ: **{sc.MucDoKhanCap}** (Trạng thái: *{sc.TrangThai}*)");
                    }
                }
                else
                {
                    sbSc.AppendLine("✅ Toàn bộ sự cố cơ sở vật chất đã được xử lý xong.");
                }

                return Json(new
                {
                    response = sbSc.ToString().Trim(),
                    source = "Quản trị KTX - Báo cáo Sự cố",
                    quickAction = new { label = "🚨 Xem và Duyệt sự cố ngay", prompt = "Duyệt sự cố kỹ thuật đang chờ xử lý", mode = "ACTION" }
                });
            }

            // F. QUẢN LÝ CƠ SỞ VẬT CHẤT
            if (intent == "CO_SO_VAT_CHAT")
            {
                if (mentionedRooms.Any())
                {
                    string targetRoom = mentionedRooms.First();
                    var items = await _context.CoSoVatChats.Where(c => c.MaPhong == targetRoom).AsNoTracking().ToListAsync();
                    var sbC = new StringBuilder();
                    sbC.AppendLine($"🛋 **Danh mục Thiết bị & Cơ sở vật chất Phòng P.{targetRoom}:**\n");
                    foreach (var it in items)
                    {
                        sbC.AppendLine($"• **{it.TenThietBi}** (Mã: `{it.MaThietBi}`) — SL: {it.SoLuong} — Tình trạng: *{it.TinhTrang}*");
                    }
                    return Json(new { response = sbC.ToString().Trim(), source = "Quản trị KTX - Kiểm kê Thiết bị" });
                }

                var allItems = await _context.CoSoVatChats.AsNoTracking().ToListAsync();
                var broken = allItems.Where(c => c.TinhTrang != "Tốt" && c.TinhTrang != "Đang sử dụng" && c.TinhTrang != "Mới").ToList();

                string csvcMsg = $"🛋 **THỐNG KÊ CƠ SỞ VẬT CHẤT TOÀN KÝ TÚC XÁ:**\n\n" +
                                 $"• Tổng số thiết bị quản lý: **{allItems.Count}** hạng mục\n" +
                                 $"• Đang hoạt động bình thường: **{allItems.Count - broken.Count}**\n" +
                                 $"• Cần bảo dưỡng / sửa chữa: **{broken.Count}** thiết bị\n\n" +
                                 $"👉 Bạn có thể tra cứu thiết bị chi tiết theo từng phòng (VD: *\"Thiết bị phòng 101\"*).";

                return Json(new { response = csvcMsg, source = "Quản trị KTX - Cơ sở vật chất" });
            }

            // G. QUẢN LÝ KỶ LUẬT & AN NINH
            if (intent == "KY_LUAT")
            {
                var viPhams = await _context.ViPhams
                    .Include(v => v.SinhVien)
                    .OrderByDescending(v => v.NgayViPham)
                    .Take(8)
                    .ToListAsync();

                int totalVp = await _context.ViPhams.CountAsync();
                var sbVp = new StringBuilder();
                sbVp.AppendLine($"⚠️ **BÁO CÁO TÌNH HÌNH KỶ LUẬT SINH VIÊN (Tổng cộng: {totalVp} biên bản):**\n");
                foreach (var vp in viPhams)
                {
                    sbVp.AppendLine($"• **{vp.MaSV} - {vp.SinhVien?.HoTen}** ({vp.NgayViPham:dd/MM/yyyy}): {vp.NoiDungViPham} — Hình thức: **{vp.HinhThucXuLy}**");
                }

                return Json(new { response = sbVp.ToString().Trim(), source = "Quản trị KTX - Kỷ luật & An ninh" });
            }

            // H. TRA CỨU SINH VIÊN
            if (intent == "SINH_VIEN")
            {
                if (matchSv.Success)
                {
                    var sv = await _context.SinhViens.FirstOrDefaultAsync(s => s.MaSV == matchSv.Value);
                    if (sv != null)
                    {
                        var hd = await _context.HopDongs.FirstOrDefaultAsync(h => h.MaSV == sv.MaSV && h.TrangThai == "Còn hiệu lực");
                        var unpaidCount = await _context.HoaDons.CountAsync(h => h.MaSV == sv.MaSV && h.TrangThai != "Đã thanh toán");

                        string svInfo = $"👨‍🎓 **Hồ sơ sinh viên {sv.HoTen} ({sv.MaSV}):**\n" +
                                        $"• Lớp: {sv.Lop} | SĐT: {sv.SoDienThoai}\n" +
                                        $"• Phòng đang ở: **P.{sv.MaPhong ?? "Chưa xếp"}** (Trạng thái: {sv.TinhTrangLuuTru})\n" +
                                        $"• Hợp đồng: {(hd != null ? $"HĐ {hd.MaHopDong} (Hiệu lực đến {hd.NgayKetThuc:dd/MM/yyyy})" : "Chưa có HĐ hiệu lực")}\n" +
                                        $"• Hóa đơn nợ đọng: **{unpaidCount}** hóa đơn";
                        return Json(new { response = svInfo, source = "Quản trị KTX - Tra cứu Sinh viên" });
                    }
                }

                var countSv = await _context.SinhViens.CountAsync(s => s.TinhTrangLuuTru == "Đang ở");
                return Json(new
                {
                    response = $"👨‍🎓 Hiện có **{countSv} sinh viên** đang lưu trú tại KTX. Bạn có thể tra cứu chi tiết bằng cách nhập Mã sinh viên (VD: *\"DTC001\"*).",
                    source = "Quản trị KTX - Tra cứu Sinh viên"
                });
            }

            // I. NỘI QUY & QUY CHẾ VẬN HÀNH KTX (AI PROMPT RAG DÀNH CHO ADMIN)
            return await AnswerAdminFromKnowledgeAsync(rawMsg, msgLower, intent);
        }

        private async Task<IActionResult> AnswerAdminFromKnowledgeAsync(string rawMsg, string msgLower, string intent)
        {
            string matchedFacts = RetrievePolicyFacts(rawMsg, msgLower);
            if (string.IsNullOrWhiteSpace(matchedFacts))
            {
                // Không có nội quy nào liên quan (câu ngoài phạm vi KTX) -> không gửi cho LLM để tránh tự bịa
                if (!GetFactListByIntent(intent).Any())
                    return Json(new { response = GetFactsByIntent(intent), source = "Quản trị KTX - Cơ sở tri thức & Quy chế" });
                matchedFacts = GetFactsByIntent(intent);
            }
            string adminPrompt = AIPrompts.BuildAdminRagPrompt(rawMsg, matchedFacts);
            string? aiResponse = CleanAiOutput(await CallOllamaOrNullAsync(adminPrompt));

            return Json(new
            {
                response = aiResponse ?? matchedFacts,
                source = "Quản trị KTX - Cơ sở tri thức & Quy chế"
            });
        }

        // =========================================================================
        // LUỒNG XỬ LÝ DÀNH RIÊNG CHO SINH VIÊN (STUDENT RESIDENT ASSISTANT)
        // =========================================================================
        private async Task<IActionResult> HandleStudentFlowAsync(string rawMsg, string msgLower, string mode)
        {
            // 1. Kiểm tra và chặn sinh viên truy cập số liệu quản trị nội bộ
            bool isOwnData = msgLower.Contains("của tôi") || msgLower.Contains("của mình") || msgLower.Contains("phòng tôi") || msgLower.Contains("tôi có") || msgLower.Contains("mình có");
            bool isUnauthorized = !isOwnData && (
                msgLower.Contains("doanh thu") ||
                msgLower.Contains("tổng số tiền thu") ||
                msgLower.Contains("danh sách nợ") ||
                msgLower.Contains("toàn bộ sinh viên nợ") ||
                msgLower.Contains("sinh viên nợ") ||
                msgLower.Contains("công nợ") ||
                msgLower.Contains("danh sách tất cả sinh viên vi phạm") ||
                msgLower.Contains("toàn bộ vi phạm") ||
                msgLower.Contains("danh sách vi phạm") ||
                msgLower.Contains("thống kê") ||
                msgLower.Contains("báo cáo hệ thống") ||
                msgLower.Contains("báo cáo ktx")
            );

            if (isUnauthorized)
            {
                return Json(new
                {
                    response = "🔒 **Thông báo quyền hạn:** Bạn đang đăng nhập với vai trò **Sinh viên**. " +
                               "Dữ liệu doanh thu, công nợ toàn hệ thống và hồ sơ riêng tư của các sinh viên khác thuộc thẩm quyền quản trị của Ban Quản lý KTX. " +
                               "Bạn chỉ có thể tra cứu hóa đơn, hợp đồng và dịch vụ của chính phòng mình.",
                    source = "Cổng thông tin Sinh viên KTX"
                });
            }

            // 2. Chế độ Action (Nhờ AI làm hộ cho sinh viên)
            if (mode == "ACTION")
            {
                string actionIntent = DetectActionIntent(msgLower, forceActionMode: true) ?? "ACTION_HUB";
                var actionResult = await HandleActionAgentAsync(actionIntent, rawMsg, msgLower, isExplicitActionMode: true);
                if (actionResult != null)
                    return actionResult;
            }
            else
            {
                // TỰ ĐỘNG PHÁT HIỆN HÀNH ĐỘNG:
                // Người dùng không cần chuyển tab: nếu câu gõ mang ý định nhờ AI làm hộ
                // (VD: "thanh toán hóa đơn cho tôi", "gia hạn phòng", "báo hỏng bóng đèn"...),
                // AI sẽ tự động kích hoạt hành động ngay!
                string? autoActionIntent = DetectActionIntent(msgLower, forceActionMode: false);
                if (!string.IsNullOrEmpty(autoActionIntent))
                {
                    var actionResult = await HandleActionAgentAsync(autoActionIntent, rawMsg, msgLower, isExplicitActionMode: false);
                    if (actionResult != null)
                        return actionResult;
                }
            }

            // 3. Chế độ Hỏi đáp cá nhân của sinh viên
            return await HandleQaModeAsync(rawMsg, msgLower);
        }

        private async Task<IActionResult> HandleQaModeAsync(string rawMsg, string msgLower)
        {
            var (currentSv, activeMaPhong, allRooms, mentionedRooms) = await ResolveStudentContextAsync(rawMsg);
            string svTag = currentSv != null ? $"**{currentSv.HoTen} ({currentSv.MaSV} - Phòng P.{currentSv.MaPhong ?? activeMaPhong})**" : $"**Phòng P.{activeMaPhong}**";

            // Xác định chủ đề Hỏi đáp ưu tiên theo từ khóa ngữ nghĩa
            string intent = ClassifyQaIntent(msgLower);

            // Câu hỏi thuần về nội quy/biểu phí -> trả lời từ cơ sở tri thức, không tra hóa đơn/hợp đồng cá nhân
            if (IsPolicyOnlyQuestion(msgLower, mentionedRooms.Any()))
                return await AnswerStudentFromKnowledgeAsync(rawMsg, msgLower, intent, svTag);

            // Nếu chưa rõ từ khóa, gọi Ollama phân tích bổ trợ (nếu có)
            if (intent == "KHAC")
            {
                string classifyPrompt = $@"Phân tích câu hỏi và trả về duy nhất định dạng JSON thuần túy không kèm giải thích:
{{
  ""intent"": ""HOADON_DIEN_NUOC"" | ""HOP_DONG"" | ""PHONG"" | ""SINH_VIEN"" | ""SU_CO"" | ""CO_SO_VAT_CHAT"" | ""KY_LUAT"" | ""THONG_KE"" | ""XIN_CHAO"" | ""GIOGIAC_NOIQUY"" | ""GUI_XE_RA_VAO"" | ""DANG_KY_TRA_PHONG"" | ""TIEP_KHACH_BAN_BE"" | ""KHAC""
}}
Câu hỏi: ""{rawMsg}""";
                string? jsonIntentResponse = await CallOllamaOrNullAsync(classifyPrompt);
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

                // Câu không có từ khóa nào và cũng không nhắc tới dữ liệu cá nhân (VD: "wifi có mất phí không"):
                // dù LLM đoán chủ đề gì cũng trả lời từ nội quy, tránh trả về hóa đơn/hợp đồng của sinh viên
                if (!HasDataReference(msgLower, mentionedRooms.Any()))
                    return await AnswerStudentFromKnowledgeAsync(rawMsg, msgLower, intent, svTag);
            }

            // ---------------------------------------------------------------------
            // 1. HỎI ĐÁP VỀ HÓA ĐƠN, TIỀN PHÒNG, ĐIỆN NƯỚC (LIỆT KÊ CHI TIẾT HÓA ĐƠN CHƯA THANH TOÁN / ĐÃ THANH TOÁN)
            // ---------------------------------------------------------------------
            if (intent == "HOADON_DIEN_NUOC")
            {
                try
                {
                    // Nếu hỏi thuần về bảng đơn giá điện nước quy định
                    if ((msgLower.Contains("giá điện") || msgLower.Contains("giá nước") || msgLower.Contains("bao nhiêu 1 số") || msgLower.Contains("đơn giá"))
                        && !msgLower.Contains("của tôi") && !msgLower.Contains("còn") && !msgLower.Contains("chưa"))
                    {
                        string tariffMsg = "⚡💧 **Đơn giá Điện - Nước & Dịch vụ quy định tại Ký túc xá:**\n" +
                                           "• **Điện sinh hoạt:** **2.500 VNĐ / kWh** (tính theo công thức: `(Chỉ số mới - Chỉ số cũ) × Đơn giá`).\n" +
                                           "• **Nước sạch:** **15.000 VNĐ / m³**.\n" +
                                           "• **Internet Wifi & Vệ sinh hành lang:** Miễn phí 100%.\n" +
                                           "• **Phí gửi xe máy:** **80.000 VNĐ / tháng**.\n" +
                                           "• **Phí gửi xe đạp điện / xe máy điện (kèm sạc an toàn):** **130.000 VNĐ / tháng**.\n" +
                                           "• **Chu kỳ chốt số:** Ngày 25 đến 28 hàng tháng, hạn thanh toán trên Student Portal hoặc tại Văn phòng BQL.";
                        return Json(new
                        {
                            response = tariffMsg,
                            source = "Hỏi đáp - Quản lý Điện nước & Hóa đơn",
                            quickAction = new { label = "⚡ Xem và mở phiếu thanh toán hóa đơn", prompt = "Thanh toán hóa đơn cho tôi", mode = "ACTION" }
                        });
                    }

                    // BẢO MẬT (Chống BOLA/IDOR): Sinh viên chỉ được tra cứu hóa đơn của chính mình/phòng mình
                    if (currentSv != null && mentionedRooms.Any() && !string.Equals(mentionedRooms.First(), currentSv.MaPhong, StringComparison.OrdinalIgnoreCase))
                    {
                        return Json(new
                        {
                            response = $"🔒 **Thông báo quyền riêng tư:** Bạn đang đăng nhập với tư cách sinh viên phòng **P.{currentSv.MaPhong}**. Bạn chỉ có quyền tra cứu hóa đơn và dịch vụ của chính phòng mình. Vui lòng liên hệ Ban Quản lý nếu cần hỗ trợ thông tin phòng khác.",
                            source = "Cổng thông tin Sinh viên KTX"
                        });
                    }

                    var query = _context.HoaDons.Include(h => h.SinhVien).AsNoTracking().AsQueryable();
                    if (currentSv != null)
                    {
                        query = query.Where(h => h.MaSV == currentSv.MaSV || h.MaPhong == currentSv.MaPhong);
                    }
                    else if (mentionedRooms.Any())
                    {
                        string r = mentionedRooms.First();
                        query = query.Where(h => h.MaPhong == r);
                    }

                    var hoaDons = await query.OrderByDescending(h => h.NgayLap).ToListAsync();
                    var chuaThanhToan = hoaDons.Where(h => h.TrangThai != "Đã thanh toán").ToList();
                    var daThanhToan = hoaDons.Where(h => h.TrangThai == "Đã thanh toán").ToList();

                    var sb = new StringBuilder();
                    sb.AppendLine($"🧾 **Kết quả tra cứu Hóa đơn của {svTag}:**\n");

                    if (chuaThanhToan.Any())
                    {
                        decimal tongNo = chuaThanhToan.Sum(h => h.TongTien);
                        sb.AppendLine($"⚠️ **Bạn hiện có {chuaThanhToan.Count} hóa đơn CHƯA THANH TOÁN (Tổng cộng: {tongNo:N0} VNĐ):**");
                        int idx = 1;
                        foreach (var hd in chuaThanhToan)
                        {
                            string chiSoText = (hd.ChiSoCu.HasValue && hd.ChiSoMoi.HasValue)
                                ? $" (Chỉ số: {hd.ChiSoCu} → {hd.ChiSoMoi}, tiêu thụ {hd.ChiSoMoi - hd.ChiSoCu} số)"
                                : "";
                            sb.AppendLine($"{idx}. Mã HĐ: **{hd.MaHoaDon}** — Loại: **{hd.LoaiHoaDon}** (P.{hd.MaPhong}){chiSoText}");
                            sb.AppendLine($"   • Số tiền: **{hd.TongTien:N0} VNĐ** | Ngày lập: {hd.NgayLap:dd/MM/yyyy} | Trạng thái: *{hd.TrangThai}*");
                            idx++;
                        }
                    }
                    else
                    {
                        sb.AppendLine("✅ **Tuyệt vời! Bạn hiện KHÔNG CÒN hóa đơn nào chưa thanh toán.** Toàn bộ tiền phòng và điện nước đã được đóng đầy đủ.");
                    }

                    if (daThanhToan.Any() && (!msgLower.Contains("chưa thanh toán") || !chuaThanhToan.Any()))
                    {
                        sb.AppendLine($"\n✅ **Lịch sử hóa đơn ĐÃ THANH TOÁN gần đây ({daThanhToan.Count} hóa đơn):**");
                        foreach (var hd in daThanhToan.Take(4))
                        {
                            sb.AppendLine($"• **{hd.MaHoaDon}** ({hd.LoaiHoaDon} - P.{hd.MaPhong}): **{hd.TongTien:N0} VNĐ** — *Đã thanh toán qua {hd.HinhThucThanhToan}* ({hd.NgayLap:dd/MM/yyyy})");
                        }
                    }

                    object? hint = chuaThanhToan.Any()
                        ? new { label = "⚡ Mở phiếu thanh toán hóa đơn", prompt = "Thanh toán hóa đơn cho tôi", mode = "ACTION" }
                        : null;

                    return Json(new
                    {
                        response = sb.ToString().Trim(),
                        source = "Hỏi đáp - Tra cứu Hóa đơn CSDL",
                        quickAction = hint
                    });
                }
                catch (Exception ex) { _logger.LogError(ex, "Lỗi tra cứu Hóa đơn"); }
            }

            // ---------------------------------------------------------------------
            // 2. HỎI ĐÁP VỀ HỢP ĐỒNG & THỜI HẠN LƯU TRÚ (LIỆT KÊ HỢP ĐỒNG CỦA TÔI / TOÀN KTX)
            // ---------------------------------------------------------------------
            if (intent == "HOP_DONG")
            {
                try
                {
                    var query = _context.HopDongs.Include(h => h.SinhVien).Include(h => h.Phong).AsNoTracking().AsQueryable();
                    if (currentSv != null && !msgLower.Contains("tất cả") && !msgLower.Contains("toàn bộ") && !msgLower.Contains("bao nhiêu hợp đồng"))
                    {
                        var myContracts = await query.Where(h => h.MaSV == currentSv.MaSV).OrderByDescending(h => h.NgayKetThuc).ToListAsync();
                        if (myContracts.Any())
                        {
                            var sb = new StringBuilder();
                            sb.AppendLine($"📄 **Thông tin Hợp đồng lưu trú của {svTag}:**\n");
                            foreach (var hd in myContracts)
                            {
                                int daysLeft = (hd.NgayKetThuc.Date - DateTime.Now.Date).Days;
                                string timeStatus = daysLeft >= 0
                                    ? $"Còn **{daysLeft} ngày** nữa hết hạn"
                                    : $"Đã quá hạn **{-daysLeft} ngày**";

                                sb.AppendLine($"• Mã hợp đồng: **{hd.MaHopDong}**");
                                sb.AppendLine($"• Phòng đăng ký: **P.{hd.MaPhong}** ({hd.Phong?.LoaiPhong ?? "Tiêu chuẩn"})");
                                sb.AppendLine($"• Thời hạn: Từ **{hd.NgayBatDau:dd/MM/yyyy}** đến **{hd.NgayKetThuc:dd/MM/yyyy}** ({timeStatus})");
                                sb.AppendLine($"• Tiền đặt cọc: **{hd.TienDatCoc:N0} VNĐ**");
                                sb.AppendLine($"• Trạng thái: **{hd.TrangThai}**");
                                if (!string.IsNullOrWhiteSpace(hd.DieuKhoan))
                                    sb.AppendLine($"• Ghi chú / Điều khoản: *{hd.DieuKhoan}*");
                                sb.AppendLine();
                            }

                            return Json(new
                            {
                                response = sb.ToString().Trim(),
                                source = "Hỏi đáp - Tra cứu Hợp đồng CSDL",
                                quickAction = new { label = "⚡ Nhờ AI gia hạn phòng cho tôi ngay (+6 tháng)", prompt = "Gia hạn phòng cho tôi", mode = "ACTION" }
                            });
                        }
                    }

                    if (currentSv != null)
                    {
                        return Json(new
                        {
                            response = "Bạn hiện không có hợp đồng lưu trú nào khác trên hệ thống. Vui lòng liên hệ Ban Quản lý KTX nếu cần lập mới hoặc đối soát hợp đồng.",
                            source = "Cổng thông tin Sinh viên KTX"
                        });
                    }

                    int conHieuLuc = await _context.HopDongs.CountAsync(h => h.TrangThai == "Còn hiệu lực");
                    int hetHieuLuc = await _context.HopDongs.CountAsync(h => h.TrangThai != "Còn hiệu lực");
                    var sampleList = await _context.HopDongs.Include(h => h.SinhVien).AsNoTracking().OrderByDescending(h => h.NgayKetThuc).Take(5).ToListAsync();

                    var sbAll = new StringBuilder();
                    sbAll.AppendLine($"📄 **Thống kê Hợp đồng Ký túc xá:**");
                    sbAll.AppendLine($"• Hợp đồng đang còn hiệu lực: **{conHieuLuc}** hợp đồng");
                    sbAll.AppendLine($"• Hợp đồng hết hạn / đã thanh lý: **{hetHieuLuc}** hợp đồng");
                    sbAll.AppendLine($"• Tiền đặt cọc tiêu chuẩn: **1.000.000 VNĐ / hợp đồng**\n");
                    sbAll.AppendLine("📋 **Một số hợp đồng tiêu biểu trên hệ thống:**");
                    foreach (var h in sampleList)
                    {
                        sbAll.AppendLine($"• **{h.MaHopDong}** — SV: {h.SinhVien?.HoTen ?? h.MaSV} (P.{h.MaPhong}): {h.NgayBatDau:dd/MM/yyyy} → {h.NgayKetThuc:dd/MM/yyyy} (*{h.TrangThai}*)");
                    }

                    return Json(new
                    {
                        response = sbAll.ToString().Trim(),
                        source = "Hỏi đáp - Quản lý Hợp đồng",
                        quickAction = new { label = "⚡ Nhờ AI gia hạn phòng cho tôi", prompt = "Gia hạn phòng cho tôi", mode = "ACTION" }
                    });
                }
                catch (Exception ex) { _logger.LogError(ex, "Lỗi tra cứu Hợp đồng"); }
            }

            // ---------------------------------------------------------------------
            // 3. HỎI ĐÁP VỀ PHÒNG Ở, BẠN CÙNG PHÒNG, PHÒNG TRỐNG & GIÁ PHÒNG
            // ---------------------------------------------------------------------
            if (intent == "PHONG")
            {
                try
                {
                    var phongs = await _context.Phongs.Include(p => p.SinhViens).AsNoTracking().ToListAsync();

                    // Nếu hỏi về phòng của bản thân hoặc phòng cụ thể ("phòng tôi có ai", "bạn cùng phòng", "thông tin phòng 101")
                    bool askMyRoom = msgLower.Contains("phòng tôi") || msgLower.Contains("phòng của tôi") ||
                                     msgLower.Contains("bạn cùng phòng") || msgLower.Contains("ở cùng") ||
                                     msgLower.Contains("thành viên") || msgLower.Contains("có những ai") ||
                                     msgLower.Contains("có ai ở") || mentionedRooms.Any();

                    if (askMyRoom && !msgLower.Contains("phòng trống") && !msgLower.Contains("còn chỗ"))
                    {
                        string targetRoomCode = mentionedRooms.FirstOrDefault() ?? activeMaPhong;
                        var roomObj = phongs.FirstOrDefault(p => p.MaPhong.Equals(targetRoomCode, StringComparison.OrdinalIgnoreCase));
                        if (roomObj != null)
                        {
                            var roommates = roomObj.SinhViens?.Where(s => s.TinhTrangLuuTru == "Đang ở").ToList() ?? new List<SinhVien>();
                            int giuongTrong = Math.Max(0, roomObj.SoLuongGiuong - roommates.Count);

                            var sbRoom = new StringBuilder();
                            sbRoom.AppendLine($"🏢 **Thông tin chi tiết Phòng P.{roomObj.MaPhong}:**");
                            sbRoom.AppendLine($"• Loại phòng: **{roomObj.LoaiPhong}** | Giá niêm yết: **{roomObj.GiaPhong:N0} VNĐ/tháng/SV** (hoặc {(roomObj.GiaPhong * 5):N0} VNĐ/kỳ 5 tháng)");
                            sbRoom.AppendLine($"• Sức chứa: **{roommates.Count}/{roomObj.SoLuongGiuong} sinh viên** (Còn trống **{giuongTrong} giường**) — Trạng thái: **{roomObj.TinhTrang}**");
                            sbRoom.AppendLine($"• Tình trạng thiết bị: {roomObj.TrangThaiThietBi ?? "Bình thường"}\n");

                            bool isMyOwnRoom = currentSv != null && !string.IsNullOrWhiteSpace(currentSv.MaPhong) && string.Equals(roomObj.MaPhong, currentSv.MaPhong, StringComparison.OrdinalIgnoreCase);
                            if (roommates.Any())
                            {
                                if (isMyOwnRoom)
                                {
                                    sbRoom.AppendLine($"👥 **Danh sách {roommates.Count} sinh viên đang ở tại Phòng P.{roomObj.MaPhong} (Cùng phòng bạn):**");
                                    int i = 1;
                                    foreach (var rm in roommates)
                                    {
                                        string meBadge = (currentSv != null && rm.MaSV == currentSv.MaSV) ? " *(Bạn)*" : "";
                                        sbRoom.AppendLine($"{i}. **{rm.HoTen}**{meBadge} — Mã SV: **{rm.MaSV}** | Lớp: {rm.Lop}");
                                        i++;
                                    }
                                }
                                else
                                {
                                    sbRoom.AppendLine("👥 **Thành viên phòng:** Vì chính sách bảo mật thông tin cá nhân (PII), danh sách chi tiết các sinh viên đang lưu trú tại phòng khác không được công khai.");
                                }
                            }
                            else
                            {
                                sbRoom.AppendLine("📭 Hiện tại phòng này chưa có sinh viên nào đang lưu trú.");
                            }

                            return Json(new
                            {
                                response = sbRoom.ToString().Trim(),
                                source = "Hỏi đáp - Tra cứu Phòng & Thành viên",
                                quickAction = new { label = "⚡ Nhờ AI làm đơn chuyển phòng", prompt = "Xin chuyển phòng cho tôi", mode = "ACTION" }
                            });
                        }
                    }

                    // Nếu hỏi về bảng giá phòng
                    if (msgLower.Contains("giá"))
                    {
                        string giaMsg = "💰 **Bảng giá phòng Ký túc xá niêm yết (tính theo đầu Sinh viên):**\n" +
                                        "• **Phòng thường (quạt trần, 6-8 người):** 350.000 VNĐ / sinh viên / tháng (hoặc 1.750.000 VNĐ / kỳ 5 tháng)\n" +
                                        "• **Phòng có điều hoà (4-6 người):** 650.000 VNĐ / sinh viên / tháng (hoặc 3.250.000 VNĐ / kỳ 5 tháng)\n" +
                                        "• **Phòng điều hoà + nóng lạnh (4 người):** 950.000 VNĐ / sinh viên / tháng (hoặc 4.750.000 VNĐ / kỳ 5 tháng)\n" +
                                        "• **Tiền đặt cọc hợp đồng:** 1.000.000 VNĐ / hợp đồng (hoàn trả khi thanh lý hợp đồng).\n" +
                                        $"• Hệ thống hiện có **{phongs.Count(p => p.TinhTrang == "Còn chỗ")}/{phongs.Count} phòng** đang còn giường trống.";
                        return Json(new { response = giaMsg, source = "Hỏi đáp - Quản lý Phòng" });
                    }

                    // Liệt kê danh sách phòng còn trống
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
                        .ToList();

                    var sbEmpty = new StringBuilder();
                    sbEmpty.AppendLine($"🏢 **Hệ thống hiện có {phongConCho.Count}/{phongs.Count} phòng còn giường trống:**\n");
                    foreach (var p in phongConCho.Take(8))
                    {
                        sbEmpty.AppendLine($"• **Phòng P.{p.MaPhong}** ({p.LoaiPhong}): Trống **{p.GiuongTrong}/{p.SoLuongGiuong} giường** — Giá: **{p.GiaPhong:N0} VNĐ/tháng/SV**");
                    }

                    return Json(new
                    {
                        response = sbEmpty.ToString().Trim(),
                        source = "Hỏi đáp - Tra cứu Phòng trống",
                        quickAction = new { label = "⚡ Nhờ AI chuyển sang phòng mới", prompt = "Xin chuyển phòng cho tôi", mode = "ACTION" }
                    });
                }
                catch (Exception ex) { _logger.LogError(ex, "Lỗi tra cứu Phòng"); }
            }

            // ---------------------------------------------------------------------
            // 4. HỎI ĐÁP VỀ SỰ CỐ, BÁO HỎNG & TIẾN ĐỘ ĐƠN YÊU CẦU
            // ---------------------------------------------------------------------
            if (intent == "SU_CO" || intent == "BAO_HONG_THIET_BI")
            {
                try
                {
                    // Nếu hỏi quy trình hướng dẫn thuần túy
                    if ((msgLower.StartsWith("hướng dẫn") || msgLower.StartsWith("quy trình") || msgLower.StartsWith("cách "))
                        && !msgLower.Contains("của tôi") && !msgLower.Contains("phòng tôi"))
                    {
                        string guide = GetFactsByIntent("BAO_HONG_THIET_BI");
                        return Json(new
                        {
                            response = "🛠 **Quy trình báo hỏng & sửa chữa thiết bị KTX:**\n" + guide,
                            source = "Hỏi đáp - Quy định Kỹ thuật KTX",
                            quickAction = new { label = "⚡ Nhờ AI báo hỏng thiết bị hộ tôi ngay", prompt = "Báo hỏng thiết bị phòng tôi", mode = "ACTION" }
                        });
                    }

                    var query = _context.BaoCaoSuCos.AsNoTracking().AsQueryable();
                    bool isPersonal = msgLower.Contains("tôi") || msgLower.Contains("mình") || mentionedRooms.Any();

                    if (mentionedRooms.Any())
                    {
                        string r = mentionedRooms.First();
                        query = query.Where(b => b.MaPhong == r);
                    }
                    else if (isPersonal && currentSv != null)
                    {
                        query = query.Where(b => b.MaSV == currentSv.MaSV || b.MaPhong == currentSv.MaPhong);
                    }

                    var list = await query.OrderByDescending(b => b.NgayBaoCao).Take(10).ToListAsync();
                    if (!list.Any())
                    {
                        return Json(new
                        {
                            response = $"✅ Hiện tại {svTag} **không có sự cố hay đơn yêu cầu nào** đang tồn đọng trên hệ thống.",
                            source = "Hỏi đáp - Tra cứu Sự cố & Đơn từ",
                            quickAction = new { label = "⚡ Nhờ AI tạo báo cáo sự cố mới", prompt = "Báo cáo sự cố hộ tôi", mode = "ACTION" }
                        });
                    }

                    int choXuLy = list.Count(x => x.TrangThai == "Chờ xử lý");
                    int daXuLy = list.Count(x => x.TrangThai == "Đã xử lý");

                    var sbSc = new StringBuilder();
                    sbSc.AppendLine($"🔧 **Danh sách phiếu Báo cáo sự cố / Yêu cầu ({list.Count} phiếu gần nhất — Chờ xử lý: {choXuLy}, Đã xử lý: {daXuLy}):**\n");
                    foreach (var item in list)
                    {
                        string icon = item.TrangThai == "Đã xử lý" ? "✅" : (item.TrangThai == "Đang xử lý" ? "⏳" : "🕒");
                        sbSc.AppendLine($"• {icon} **{item.MaSuCo}** (P.{item.MaPhong}): **{item.TenSuCo}**");
                        sbSc.AppendLine($"   Mức độ: {item.MucDoKhanCap} | Ngày gửi: {item.NgayBaoCao:dd/MM/yyyy HH:mm} | Trạng thái: **{item.TrangThai}**");
                    }

                    return Json(new
                    {
                        response = sbSc.ToString().Trim(),
                        source = "Hỏi đáp - Tra cứu Sự cố & Tiến độ",
                        quickAction = new { label = "⚡ Nhờ AI gửi báo cáo sự cố mới", prompt = "Báo cáo sự cố hộ tôi", mode = "ACTION" }
                    });
                }
                catch (Exception ex) { _logger.LogError(ex, "Lỗi tra cứu Sự cố"); }
            }

            // ---------------------------------------------------------------------
            // 5. HỎI ĐÁP VỀ CƠ SỞ VẬT CHẤT & TRANG THIẾT BỊ TRONG PHÒNG
            // ---------------------------------------------------------------------
            if (intent == "CO_SO_VAT_CHAT")
            {
                try
                {
                    string targetRoom = mentionedRooms.FirstOrDefault() ?? activeMaPhong;
                    var roomItems = await _context.CoSoVatChats
                        .Where(c => c.MaPhong == targetRoom)
                        .AsNoTracking()
                        .ToListAsync();

                    var sbCsvc = new StringBuilder();
                    if (roomItems.Any())
                    {
                        sbCsvc.AppendLine($"🛋 **Danh sách Cơ sở vật chất & Thiết bị tại Phòng P.{targetRoom} ({roomItems.Count} hạng mục):**\n");
                        foreach (var item in roomItems)
                        {
                            string statusBadge = (item.TinhTrang == "Tốt" || item.TinhTrang == "Mới" || item.TinhTrang == "Đang sử dụng")
                                ? $"✅ {item.TinhTrang}"
                                : $"⚠️ {item.TinhTrang}";
                            sbCsvc.AppendLine($"• **{item.TenThietBi}** (Mã: `{item.MaThietBi}`) — Số lượng: **{item.SoLuong}** — Tình trạng: **{statusBadge}**");
                        }
                    }
                    else
                    {
                        var allItems = await _context.CoSoVatChats.AsNoTracking().ToListAsync();
                        var canBaoTri = allItems.Where(c => c.TinhTrang != "Tốt" && c.TinhTrang != "Đang sử dụng" && c.TinhTrang != "Mới").ToList();
                        sbCsvc.AppendLine($"🛋 **Thống kê Cơ sở vật chất KTX (Phòng P.{targetRoom} sử dụng gói thiết bị tiêu chuẩn):**");
                        sbCsvc.AppendLine($"• Tổng danh mục thiết bị toàn KTX: **{allItems.Count}** thiết bị");
                        sbCsvc.AppendLine($"• Đang hoạt động tốt: **{allItems.Count - canBaoTri.Count}** | Cần bảo trì/sửa chữa: **{canBaoTri.Count}**");
                        foreach (var item in allItems.Take(6))
                        {
                            sbCsvc.AppendLine($"• **{item.TenThietBi}** (P.{item.MaPhong}) — SL: {item.SoLuong} — *{item.TinhTrang}*");
                        }
                    }

                    return Json(new
                    {
                        response = sbCsvc.ToString().Trim(),
                        source = "Hỏi đáp - Quản lý Cơ sở vật chất",
                        quickAction = new { label = "⚡ Nhờ AI báo hỏng thiết bị trong phòng", prompt = "Báo hỏng thiết bị phòng tôi", mode = "ACTION" }
                    });
                }
                catch (Exception ex) { _logger.LogError(ex, "Lỗi tra cứu CSVC"); }
            }

            // ---------------------------------------------------------------------
            // 6. HỎI ĐÁP VỀ KỶ LUẬT & VI PHẠM NỘI QUY
            // ---------------------------------------------------------------------
            if (intent == "KY_LUAT")
            {
                try
                {
                    bool askPersonal = msgLower.Contains("tôi") || msgLower.Contains("mình") || Regex.IsMatch(rawMsg.ToUpper(), @"DTC\d+");
                    if (askPersonal && currentSv != null)
                    {
                        var myViPhams = await _context.ViPhams
                            .Where(v => v.MaSV == currentSv.MaSV)
                            .OrderByDescending(v => v.NgayViPham)
                            .AsNoTracking()
                            .ToListAsync();

                        if (!myViPhams.Any())
                        {
                            return Json(new
                            {
                                response = $"✅ **Kết quả tra cứu Kỷ luật của {svTag}:**\nChúc mừng bạn! Hồ sơ của bạn **hoàn toàn trong sạch (0 vi phạm)**, chưa từng bị ghi nhận biên bản kỷ luật nào tại KTX.",
                                source = "Hỏi đáp - Quản lý Kỷ luật"
                            });
                        }

                        var sbVp = new StringBuilder();
                        sbVp.AppendLine($"⚠️ **Hồ sơ Kỷ luật của {svTag} có {myViPhams.Count} biên bản vi phạm:**\n");
                        foreach (var vp in myViPhams)
                        {
                            sbVp.AppendLine($"• Ngày **{vp.NgayViPham:dd/MM/yyyy}**: **{vp.NoiDungViPham}**");
                            sbVp.AppendLine($"  Hình thức xử lý: **{vp.HinhThucXuLy}**" + (!string.IsNullOrWhiteSpace(vp.GhiChu) ? $" (*{vp.GhiChu}*)" : ""));
                        }
                        return Json(new
                        {
                            response = sbVp.ToString().Trim(),
                            source = "Hỏi đáp - Quản lý Kỷ luật",
                            quickAction = new { label = "⚡ Nhờ AI gửi đơn giải trình vi phạm", prompt = "Gửi đơn giải trình vi phạm cho tôi", mode = "ACTION" }
                        });
                    }

                    // BẢO MẬT: Không công khai danh sách hồ sơ kỷ luật cá nhân của sinh viên khác
                    return Json(new
                    {
                        response = "⚖️ **Quy định Xử lý Kỷ luật & Vi phạm Nội quy KTX:**\n" + GetFactsByIntent("KY_LUAT"),
                        source = "Hỏi đáp - Quy định Kỷ luật KTX"
                    });
                }
                catch (Exception ex) { _logger.LogError(ex, "Lỗi tra cứu Kỷ luật"); }
            }

            // ---------------------------------------------------------------------
            // 7. HỎI ĐÁP VỀ HỒ SƠ SINH VIÊN
            // ---------------------------------------------------------------------
            if (intent == "SINH_VIEN")
            {
                try
                {
                    if ((msgLower.Contains("tôi") || msgLower.Contains("mình") || msgLower.Contains("hồ sơ") || Regex.IsMatch(rawMsg.ToUpper(), @"DTC\d+")) && currentSv != null)
                    {
                        var hdCount = await _context.HopDongs.CountAsync(h => h.MaSV == currentSv.MaSV && h.TrangThai == "Còn hiệu lực");
                        var unpaidCount = await _context.HoaDons.CountAsync(h => h.MaSV == currentSv.MaSV && h.TrangThai != "Đã thanh toán");

                        string profileMsg = $"👨‍🎓 **Thông tin hồ sơ Sinh viên lưu trú:**\n" +
                                            $"• Mã sinh viên: **{currentSv.MaSV}**\n" +
                                            $"• Họ và tên: **{currentSv.HoTen}**\n" +
                                            $"• Ngày sinh: **{currentSv.NgaySinh:dd/MM/yyyy}**\n" +
                                            $"• Lớp chuyên ngành: **{currentSv.Lop}**\n" +
                                            $"• Số điện thoại: **{currentSv.SoDienThoai}**\n" +
                                            $"• Phòng hiện tại: **P.{currentSv.MaPhong ?? "Chưa xếp"}** ({currentSv.TinhTrangLuuTru})\n" +
                                            $"• Hợp đồng hiệu lực: **{hdCount}** | Hóa đơn chưa đóng: **{unpaidCount}**";
                        return Json(new
                        {
                            response = profileMsg,
                            source = "Hỏi đáp - Hồ sơ Sinh viên",
                            quickAction = new { label = "⚡ Nhờ AI gia hạn phòng cho tôi", prompt = "Gia hạn phòng cho tôi", mode = "ACTION" }
                        });
                    }

                    // BẢO MẬT: Không công khai danh sách sinh viên nội trú của KTX ra ngoài
                    return Json(new
                    {
                        response = "🔒 **Thông báo quyền riêng tư:** Danh sách sinh viên lưu trú thuộc dữ liệu quản trị nội bộ của Ban Quản lý KTX. Bạn có thể tra cứu thông tin phòng ở cá nhân tại mục 'Thông tin cá nhân' trên Student Portal.",
                        source = "Cổng thông tin Sinh viên KTX"
                    });
                }
                catch (Exception ex) { _logger.LogError(ex, "Lỗi tra cứu Sinh viên"); }
            }

            // ---------------------------------------------------------------------
            // 8. HỎI ĐÁP THỐNG KÊ TỔNG QUAN HỆ THỐNG
            // ---------------------------------------------------------------------
            if (intent == "THONG_KE")
            {
                return Json(new
                {
                    response = "🔒 **Thông báo quyền hạn:** Số liệu thống kê và báo cáo tổng quan toàn hệ thống KTX chỉ dành riêng cho Ban Quản lý KTX. Bạn đang đăng nhập với tư cách **Sinh viên**, vui lòng theo dõi thông tin phòng ở và dịch vụ cá nhân tại Student Portal.",
                    source = "Cổng thông tin Sinh viên KTX"
                });
            }

            // ---------------------------------------------------------------------
            // 9. TRA CỨU NỘI QUY & TRI THỨC CHUNG (SEMANTIC SIMILARITY RETRIEVAL / RAG)
            // ---------------------------------------------------------------------
            return await AnswerStudentFromKnowledgeAsync(rawMsg, msgLower, intent, svTag);
        }

        private async Task<IActionResult> AnswerStudentFromKnowledgeAsync(string rawMsg, string msgLower, string intent, string svTag)
        {
            string matchedFacts = RetrievePolicyFacts(rawMsg, msgLower);
            if (string.IsNullOrWhiteSpace(matchedFacts))
            {
                // Không có nội quy nào liên quan (câu ngoài phạm vi KTX) -> không gửi cho LLM để tránh tự bịa
                if (!GetFactListByIntent(intent).Any())
                    return Json(new { response = GetFactsByIntent(intent), source = "Hỏi đáp - Cơ sở tri thức KTX (Semantic RAG)" });
                matchedFacts = GetFactsByIntent(intent);
            }

            string finalPrompt = AIPrompts.BuildStudentRagPrompt(rawMsg, svTag, matchedFacts);
            string studentSysPrompt = AIPrompts.StudentChatbotSystemPrompt.Replace("{{policy_chunks}}", matchedFacts);
            string? aiResponse = CleanAiOutput(await CallOllamaOrNullAsync(finalPrompt, studentSysPrompt));

            object? quickActionHint = null;
            if (intent == "DANG_KY_TRA_PHONG" || rawMsg.Contains("trả phòng", StringComparison.OrdinalIgnoreCase))
                quickActionHint = new { label = "⚡ Nhờ AI đăng ký trả phòng hộ tôi", prompt = "Đăng ký trả phòng cho tôi", mode = "ACTION" };

            return Json(new { response = aiResponse ?? matchedFacts, source = "Hỏi đáp - Cơ sở tri thức KTX (Semantic RAG)", quickAction = quickActionHint });
        }

        private string ClassifyQaIntent(string msgLower)
        {
            if (msgLower.Contains("xin chào") || msgLower == "chào" || msgLower == "hi" || msgLower == "hello")
                return "XIN_CHAO";
            if (msgLower.Contains("hóa đơn") || msgLower.Contains("chưa thanh toán") || msgLower.Contains("chưa đóng") ||
                msgLower.Contains("còn nợ") || msgLower.Contains("tiền điện") || msgLower.Contains("tiền nước") ||
                msgLower.Contains("điện nước") || msgLower.Contains("giá điện") || msgLower.Contains("giá nước") ||
                msgLower.Contains("thanh toán") || msgLower.Contains("đóng tiền"))
                return "HOADON_DIEN_NUOC";
            if (msgLower.Contains("hợp đồng") || msgLower.Contains("hết hạn") || msgLower.Contains("gia hạn") ||
                msgLower.Contains("thời hạn") || msgLower.Contains("tiền cọc") || msgLower.Contains("đặt cọc"))
                return "HOP_DONG";
            if (msgLower.Contains("vi phạm") || msgLower.Contains("kỷ luật") || msgLower.Contains("bị phạt") || msgLower.Contains("biên bản"))
                return "KY_LUAT";
            if (msgLower.Contains("cơ sở vật chất") || msgLower.Contains("thiết bị") || msgLower.Contains("trang bị") ||
                msgLower.Contains("bàn ghế") || msgLower.Contains("giường tủ"))
                return "CO_SO_VAT_CHAT";
            if (msgLower.Contains("sự cố") || msgLower.Contains("báo hỏng") ||
                msgLower.Contains("sửa chữa") || msgLower.Contains("tiến độ") || msgLower.Contains("duyệt chưa"))
                return "SU_CO";
            if (msgLower.Contains("thống kê") || msgLower.Contains("tổng số") || msgLower.Contains("toàn ktx"))
                return "THONG_KE";
            if (msgLower.Contains("phòng") || msgLower.Contains("bạn cùng phòng") || msgLower.Contains("ở cùng") ||
                msgLower.Contains("có những ai") || msgLower.Contains("chỗ trống"))
                return "PHONG";
            if (msgLower.Contains("sinh viên") || msgLower.Contains("hồ sơ") || msgLower.Contains("mã sv") || msgLower.Contains("dtc"))
                return "SINH_VIEN";

            // Các chủ đề nội quy xét sau cùng: câu vừa hỏi số liệu vừa hỏi nội quy (VD: "bao nhiêu phòng trống và giờ giới nghiêm")
            // được trả số liệu trước, phần nội quy do AppendRelatedPolicy bổ sung
            if (msgLower.Contains("giờ") || msgLower.Contains("giới nghiêm") || msgLower.Contains("đóng cửa") ||
                msgLower.Contains("mở cửa") || msgLower.Contains("nội quy"))
                return "GIOGIAC_NOIQUY";
            if (msgLower.Contains("gửi xe") || msgLower.Contains("bãi xe") || msgLower.Contains("xe máy") || msgLower.Contains("xe đạp"))
                return "GUI_XE_RA_VAO";
            if (msgLower.Contains("tiếp khách") || msgLower.Contains("bạn bè") || msgLower.Contains("người thân") || msgLower.Contains("ngủ lại"))
                return "TIEP_KHACH_BAN_BE";
            if (msgLower.Contains("thủ tục trả phòng") || msgLower.Contains("quy định trả phòng"))
                return "DANG_KY_TRA_PHONG";

            return "KHAC";
        }

        // =========================================================================
        // PHÂN BIỆT CÂU HỎI NỘI QUY / BIỂU PHÍ VỚI CÂU HỎI TRA CỨU DỮ LIỆU
        // =========================================================================
        private static readonly string[] PolicyMarkers =
        {
            "giá điện", "giá nước", "đơn giá", "biểu phí", "bảng giá", "giá phòng", "giá thuê", "phí gửi xe", "giá gửi xe",
            "nội quy", "quy định", "quy chế", "giờ đóng cửa", "giờ mở cửa", "giới nghiêm", "mấy giờ", "giờ giấc",
            "thủ tục", "hướng dẫn", "như thế nào", "thế nào", "ra sao", "tìm hiểu", "là gì", "được phép", "có được",
            "chốt số", "hạn thanh toán", "tiếp khách", "ngủ lại", "gửi xe", "đóng cửa", "mở cửa", "về muộn", "về trễ",
            "mất phí", "miễn phí", "có phí", "tốn phí", "lệ phí", "wifi", "internet"
        };

        private static readonly string[] DataMarkers =
        {
            "của tôi", "của mình", "của em", "phòng tôi", "phòng mình", "phòng em", "tôi còn", "tôi đã", "tôi đang", "tôi nợ",
            "mình còn", "em còn", "chưa thanh toán", "chưa đóng", "còn nợ", "công nợ", "nợ bao nhiêu", "danh sách",
            "những ai", "có ai", "ai đang", "bao nhiêu sinh viên", "bao nhiêu phòng", "phòng trống", "còn trống", "còn chỗ",
            "sắp hết hạn", "đang chờ", "chờ xử lý", "thống kê", "tổng quan", "tổng số", "hồ sơ", "bạn cùng phòng", "ở cùng"
        };

        // Chủ đề trong knowledge_base.json và từ khóa nhận biết từng chủ đề
        private static readonly (string Topic, string[] Keywords)[] KnowledgeTopics =
        {
            ("GIOGIAC_NOIQUY", new[] { "giờ đóng", "giờ mở", "đóng cửa", "mở cửa", "giới nghiêm", "mấy giờ", "giờ giấc", "nội quy", "về muộn", "về trễ" }),
            ("HOADON_DIEN_NUOC", new[] { "điện nước", "giá điện", "tiền điện", "giá nước", "tiền nước", "số điện", "khối nước", "kwh", "chốt số", "hạn thanh toán", "hóa đơn", "wifi", "internet", "miễn phí" }),
            ("BANG_GIA_PHONG", new[] { "giá phòng", "tiền phòng", "biểu phí", "bảng giá", "giá thuê", "loại phòng" }),
            ("GUI_XE_RA_VAO", new[] { "gửi xe", "bãi xe", "để xe", "xe máy", "xe đạp" }),
            ("TIEP_KHACH_BAN_BE", new[] { "tiếp khách", "khách", "bạn bè", "người thân", "ngủ lại", "đến thăm" }),
            ("DANG_KY_TRA_PHONG", new[] { "trả phòng", "rời ktx", "chấm dứt hợp đồng" }),
            ("BAO_HONG_THIET_BI", new[] { "báo hỏng", "hỏng", "sự cố", "sửa chữa" })
        };

        // Các cách hỏi giá có từ chen giữa: "giá tiền điện nước", "giá 1 số điện", "tiền nước bao nhiêu một khối"...
        private static readonly Regex PriceQuestionPattern = new Regex(
            @"giá\s+(tiền\s+|của\s+|1\s+|một\s+)?(điện|nước|phòng|gửi xe|xe)|bao nhiêu\s+(tiền\s+)?(1|một)\s+(số|khối|kwh|m3)|(1|một)\s+(số điện|khối nước)",
            RegexOptions.Compiled);

        private static bool HasPolicyMarker(string msgLower) =>
            PolicyMarkers.Any(msgLower.Contains) || PriceQuestionPattern.IsMatch(msgLower);

        private static bool HasDataReference(string msgLower, bool mentionsEntity) =>
            mentionsEntity || Regex.IsMatch(msgLower, @"dtc\d+") || DataMarkers.Any(msgLower.Contains);

        private static bool IsPolicyOnlyQuestion(string msgLower, bool mentionsEntity) =>
            HasPolicyMarker(msgLower) && !HasDataReference(msgLower, mentionsEntity);

        private static string? TopicOfDataIntent(string intent) => intent switch
        {
            "HOADON_DIEN_NUOC" => "HOADON_DIEN_NUOC",
            "PHONG" => "BANG_GIA_PHONG",
            "SU_CO" or "CO_SO_VAT_CHAT" => "BAO_HONG_THIET_BI",
            _ => null
        };

        /// <summary>
        /// Lấy nội quy cho MỌI chủ đề được nhắc trong câu hỏi (câu hỏi gộp nhiều ý không bị mất ý nào);
        /// không khớp chủ đề nào thì dùng truy xuất TF-IDF như trước.
        /// </summary>
        private string RetrievePolicyFacts(string rawMsg, string msgLower, string? excludeTopic = null, bool allowSemantic = true)
        {
            var facts = new List<string>();
            foreach (var (topic, keywords) in KnowledgeTopics)
            {
                if (topic != excludeTopic && keywords.Any(msgLower.Contains))
                    facts.AddRange(GetFactListByIntent(topic));
            }

            if (facts.Any())
                return "• " + string.Join("\n• ", facts.Distinct());

            // Ngưỡng 0.1: dưới mức này chủ yếu là khớp "ăn theo" vô nghĩa (VD "giá vàng" khớp đơn giá nước)
            return allowSemantic ? RetrieveSemanticKnowledgeChunks(rawMsg, topK: 3, threshold: 0.1) : string.Empty;
        }

        private IActionResult AppendRelatedPolicy(IActionResult result, string rawMsg, string msgLower)
        {
            if (result is not JsonResult json || json.Value == null) return result;
            if (!HasPolicyMarker(msgLower) || !HasDataReference(msgLower, false)) return result;

            var node = JsonSerializer.SerializeToNode(json.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web)) as System.Text.Json.Nodes.JsonObject;
            if (node == null || node.ContainsKey("actionCard") || node["response"] is not System.Text.Json.Nodes.JsonValue responseNode) return result;
            if ((node["source"]?.ToString() ?? "").Contains("tri thức", StringComparison.OrdinalIgnoreCase)) return result;

            string extra = RetrievePolicyFacts(rawMsg, msgLower, TopicOfDataIntent(ClassifyQaIntent(msgLower)), allowSemantic: false);
            if (string.IsNullOrWhiteSpace(extra)) return result;

            node["response"] = responseNode.ToString() + "\n\n📘 **Nội quy liên quan đến câu hỏi của bạn:**\n" + extra;
            return Json(node);
        }

        // =========================================================================
        // BỘ NHẬN DIỆN Ý ĐỊNH HÀNH ĐỘNG (AI ACTION ROUTER)
        // =========================================================================
        private string? DetectActionIntent(string msgLower, bool forceActionMode = false)
        {
            // Nếu không ở chế độ ép buộc Action, kiểm tra xem có phải câu hỏi tra cứu thông tin không
            if (!forceActionMode)
            {
                bool isInquiryQuestion = (msgLower.StartsWith("hướng dẫn") || msgLower.StartsWith("quy định") ||
                                          msgLower.StartsWith("thủ tục") || msgLower.StartsWith("giá ") ||
                                          msgLower.Contains("bao nhiêu") || msgLower.Contains("mấy giờ") ||
                                          msgLower.Contains("nào chưa") || msgLower.Contains("khi nào") ||
                                          msgLower.Contains("bao giờ") || msgLower.Contains("có những ai") ||
                                          msgLower.Contains("là gì") || msgLower.Contains("không?"))
                                         && !ContainsStrictDelegationKeyword(msgLower);

                if (isInquiryQuestion)
                    return null;

                // Câu mang tính hỏi thông tin ("cho tôi hỏi giờ đóng cửa", "tìm hiểu thủ tục gia hạn")
                // không được hiểu thành lệnh làm hộ, trừ khi có động từ thao tác rõ ràng
                string[] infoMarkers = { "tìm hiểu", "cho tôi hỏi", "cho mình hỏi", "cho em hỏi", "hỏi về", "thông tin về",
                                         "như thế nào", "thế nào", "ra sao", "là gì", "quy định", "nội quy", "thủ tục" };
                string[] actionVerbs = { "làm hộ", "làm giúp", "báo hộ", "báo giúp", "gửi hộ", "gửi giúp", "tạo hộ", "tạo giúp",
                                         "đăng ký hộ", "đăng ký giúp", "thanh toán hộ", "thanh toán giúp", "gia hạn hộ",
                                         "làm đơn", "xin trả phòng", "xin chuyển phòng" };
                if (infoMarkers.Any(msgLower.Contains) && !actionVerbs.Any(msgLower.Contains))
                    return null;
            }

            bool hasDelegation = forceActionMode || ContainsDelegationKeyword(msgLower);

            // 1. Menu tổng hợp làm hộ
            if ((msgLower.Contains("chức năng khác") || msgLower.Contains("làm được gì") ||
                 msgLower.Contains("làm hộ được gì") || msgLower.Contains("thao tác thủ công") ||
                 msgLower.Contains("thực hiện hộ") || msgLower.Contains("làm thay") ||
                 msgLower == "nhờ ai làm hộ" || msgLower == "làm hộ tôi" || msgLower == "nhờ làm hộ") &&
                !msgLower.Contains("gia hạn") && !msgLower.Contains("sự cố") && !msgLower.Contains("báo hỏng") && !msgLower.Contains("thanh toán"))
            {
                return "ACTION_HUB";
            }

            // 2. Hành động GIA HẠN PHÒNG / GIA HẠN HỢP ĐỒNG
            if (msgLower.Contains("gia hạn") ||
                (hasDelegation && (msgLower.Contains("hợp đồng") || msgLower.Contains("ở tiếp") || msgLower.Contains("kỳ tới"))))
            {
                return "ACTION_GIA_HAN";
            }

            // 3. Hành động XIN CHUYỂN PHÒNG / ĐỔI PHÒNG
            if ((hasDelegation && (msgLower.Contains("chuyển phòng") || msgLower.Contains("đổi phòng") ||
                                   msgLower.Contains("chuyển sang") || msgLower.Contains("đổi sang"))) ||
                msgLower.Contains("xin chuyển phòng") || msgLower.Contains("muốn chuyển phòng") ||
                msgLower.Contains("muốn đổi phòng") || msgLower.Contains("làm đơn chuyển phòng") ||
                Regex.IsMatch(msgLower, @"(chuyển|đổi)\s+(từ\s+phòng.*)?sang\s+phòng"))
            {
                return "ACTION_CHUYEN_PHONG";
            }

            // 4. Hành động ĐĂNG KÝ TRẢ PHÒNG
            if ((hasDelegation && (msgLower.Contains("trả phòng") || msgLower.Contains("rời khỏi") || msgLower.Contains("ra khỏi"))) ||
                msgLower.Contains("xin trả phòng") || msgLower.Contains("muốn trả phòng") ||
                msgLower.Contains("đăng ký trả phòng") || msgLower.Contains("làm đơn trả phòng"))
            {
                return "ACTION_TRA_PHONG";
            }

            // 5. Hành động THANH TOÁN HÓA ĐƠN (TIỀN PHÒNG, ĐIỆN NƯỚC)
            if (hasDelegation && (msgLower.Contains("thanh toán") || msgLower.Contains("đóng tiền") ||
                                  msgLower.Contains("trả tiền") || msgLower.Contains("gạch nợ") ||
                                  msgLower.Contains("hóa đơn") || msgLower.Contains("tiền điện") ||
                                  msgLower.Contains("tiền nước") || msgLower.Contains("tiền phòng")))
            {
                return "ACTION_HOA_DON";
            }

            // 6. Hành động YÊU CẦU CƠ SỞ VẬT CHẤT / CẤP PHÁT THIẾT BỊ
            if (hasDelegation && (msgLower.Contains("cấp thêm") || msgLower.Contains("xin thêm") ||
                                  msgLower.Contains("bổ sung") || msgLower.Contains("thay mới") ||
                                  msgLower.Contains("mất chìa khóa") || msgLower.Contains("cơ sở vật chất")))
            {
                return "ACTION_CO_SO_VAT_CHAT";
            }

            // 7. Hành động GIẢI TRÌNH / KHIẾU NẠI KỶ LUẬT VI PHẠM
            if (hasDelegation && (msgLower.Contains("giải trình") || msgLower.Contains("khiếu nại") ||
                                  msgLower.Contains("xin giảm") || msgLower.Contains("vi phạm") || msgLower.Contains("kỷ luật")))
            {
                return "ACTION_KY_LUAT";
            }

            // 8. Hành động CẬP NHẬT SỐ ĐIỆN THOẠI / HỒ SƠ SINH VIÊN
            if (hasDelegation && (msgLower.Contains("số điện thoại") || msgLower.Contains("sđt") || msgLower.Contains("đổi số")))
            {
                return "ACTION_CAP_NHAT_SDT";
            }

            // 9. Hành động BÁO CÁO SỰ CỐ / BÁO HỎNG THIẾT BỊ HỘ
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

            if (forceActionMode)
                return "ACTION_HUB";

            return null;
        }

        private bool ContainsStrictDelegationKeyword(string msgLower)
        {
            string[] strictKeywords = new[]
            {
                "hộ tôi", "hộ mình", "giúp tôi", "giúp mình", "thay tôi", "làm hộ", "báo hộ", "thanh toán hộ", "gia hạn hộ"
            };
            return strictKeywords.Any(k => msgLower.Contains(k));
        }

        private bool ContainsDelegationKeyword(string msgLower)
        {
            string[] keywords = new[]
            {
                "hộ", "giúp", "giùm", "cho tôi", "cho mình", "thay tôi", "thay mình", "nhờ ", "làm hộ", "làm giúp",
                "báo hộ", "báo giúp", "gửi hộ", "gửi giúp", "tạo hộ", "tạo giúp",
                "đăng ký hộ", "đăng ký giúp", "thanh toán hộ", "thanh toán giúp",
                "gia hạn phòng", "tự động", "ngay tại đây", "luôn giúp"
            };
            return keywords.Any(k => msgLower.Contains(k));
        }

        // =========================================================================
        // XỬ LÝ TÁC VỤ AI ACTION AGENT (TỰ ĐỘNG THỰC THI TRÊN TẤT CẢ CHỨC NĂNG)
        // =========================================================================
        private async Task<IActionResult?> HandleActionAgentAsync(string actionIntent, string rawMsg, string msgLower, bool isExplicitActionMode = false)
        {
            var (currentSv, activeMaPhong, allRooms, mentionedRooms) = await ResolveStudentContextAsync(rawMsg);

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
                    ? $" (Hồ sơ đang kết nối: **{currentSv.HoTen} - {currentSv.MaSV}**, Phòng **P.{currentSv.MaPhong ?? activeMaPhong}**)"
                    : "";

                string hubMsg = $"⚡ **Chế độ Nhờ AI làm hộ (AI Action Agent)** đang hoạt động!{svInfo}\n\n" +
                                "Bạn chỉ cần **gõ yêu cầu bằng ngôn ngữ tự nhiên**, tôi sẽ tự động thao tác trực tiếp lên Cơ sở dữ liệu KTX cho tất cả chức năng:\n" +
                                "• 📅 **Gia hạn phòng / hợp đồng:** Gõ *\"Gia hạn phòng cho tôi\"* → Tự động gia hạn +6 tháng.\n" +
                                "• 💳 **Thanh toán hóa đơn:** Gõ *\"Thanh toán hóa đơn cho tôi\"* → Mở danh sách hóa đơn & hướng dẫn thanh toán an toàn.\n" +
                                "• 🛠 **Báo hỏng thiết bị:** Gõ *\"Báo hỏng bóng đèn phòng tôi\"* → Tự động tạo phiếu sửa chữa.\n" +
                                "• 🔄 **Chuyển phòng ở:** Gõ *\"Chuyển phòng cho tôi sang 102\"* → Tự động cập nhật phòng mới.\n" +
                                "• 🚪 **Đăng ký trả phòng:** Gõ *\"Đăng ký trả phòng cho tôi\"* → Tự động lập đơn trả phòng.\n" +
                                "• 🛋 **Yêu cầu thiết bị / Giải trình kỷ luật:** Gõ *\"Xin cấp thêm ghế cho phòng tôi\"*.\n\n" +
                                "👇 **Hoặc bấm chọn nhanh tác vụ dưới đây để tôi thực hiện ngay:**";

                return Json(new
                {
                    response = hubMsg,
                    source = "Nhờ AI làm hộ (Trợ lý Thực thi)",
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
            // B. TỰ ĐỘNG GIA HẠN PHÒNG / GIA HẠN HỢP ĐỒNG (AUTO-EXECUTE)
            // ---------------------------------------------------------------------
            if (actionIntent == "ACTION_GIA_HAN")
            {
                if (currentSv != null)
                {
                    int monthsToAdd = 6; // Mặc định 1 học kỳ (6 tháng)
                    var matchYear = Regex.Match(msgLower, @"(\d+)\s*năm");
                    var matchMonth = Regex.Match(msgLower, @"(\d+)\s*tháng");
                    if (matchYear.Success && int.TryParse(matchYear.Groups[1].Value, out int y))
                        monthsToAdd = Math.Clamp(y * 12, 1, 36);
                    else if (matchMonth.Success && int.TryParse(matchMonth.Groups[1].Value, out int m))
                        monthsToAdd = Math.Clamp(m, 1, 36);

                    var hd = await _context.HopDongs.FirstOrDefaultAsync(h => h.MaSV == currentSv.MaSV && h.TrangThai == "Còn hiệu lực")
                             ?? await _context.HopDongs.FirstOrDefaultAsync(h => h.MaSV == currentSv.MaSV);

                    string roomCode = currentSv.MaPhong ?? activeMaPhong;
                    DateTime oldEnd = hd?.NgayKetThuc ?? DateTime.Now;
                    DateTime baseDate = oldEnd < DateTime.Now ? DateTime.Now : oldEnd;
                    DateTime newEnd = baseDate.AddMonths(monthsToAdd);

                    if (hd != null)
                    {
                        hd.NgayKetThuc = newEnd;
                        hd.TrangThai = "Còn hiệu lực";
                        hd.DieuKhoan = $"Đã tự động gia hạn +{monthsToAdd} tháng qua Trợ lý AI ({DateTime.Now:dd/MM/yyyy})";
                    }
                    else
                    {
                        hd = new HopDong
                        {
                            MaHopDong = "HD_" + DateTime.Now.ToString("yyMMddHHmm"),
                            MaSV = currentSv.MaSV,
                            MaPhong = roomCode,
                            NgayBatDau = DateTime.Now,
                            NgayKetThuc = newEnd,
                            TienDatCoc = 1000000m,
                            TrangThai = "Còn hiệu lực",
                            DieuKhoan = $"Gia hạn tự động +{monthsToAdd} tháng qua Trợ lý AI"
                        };
                        _context.HopDongs.Add(hd);
                    }

                    await _context.SaveChangesAsync();

                    string autoRenewMsg = $"✅ **Đã tự động gia hạn phòng ở & hợp đồng thành công!**\n" +
                                          $"Tôi đã cập nhật trực tiếp hợp đồng **{hd.MaHopDong}** của bạn (**{currentSv.HoTen} - {currentSv.MaSV}**, Phòng **P.{roomCode}**) thêm **{monthsToAdd} tháng**.\n" +
                                          $"• Hạn cũ: {oldEnd:dd/MM/yyyy} → **Hạn mới: {newEnd:dd/MM/yyyy}**";

                    return Json(new
                    {
                        response = autoRenewMsg,
                        source = "Nhờ AI làm hộ (Đã gia hạn tự động)",
                        actionCard = new
                        {
                            cardType = "RECEIPT_CARD",
                            receipt = new
                            {
                                maPhieu = hd.MaHopDong,
                                loaiPhieu = $"Gia hạn Phòng & Hợp đồng (+{monthsToAdd} tháng)",
                                maPhong = "P." + roomCode,
                                nguoiGui = $"{currentSv.MaSV} - {currentSv.HoTen}",
                                noiDung = $"Hiệu lực mới đến ngày: {newEnd:dd/MM/yyyy}",
                                chiTiet = $"Hạn trước đó: {oldEnd:dd/MM/yyyy} | Tiền cọc: {hd.TienDatCoc:N0} VNĐ",
                                trangThai = "Đã gia hạn (Còn hiệu lực)",
                                thoiGian = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
                            }
                        }
                    });
                }
            }

            // ---------------------------------------------------------------------
            // C. TỰ ĐỘNG BÁO CÁO SỰ CỐ / BÁO HỎNG THIẾT BỊ HỘ
            // ---------------------------------------------------------------------
            if (actionIntent == "ACTION_BAO_CAO_SU_CO")
            {
                var (hasSpecificIssue, extractedTitle, extractedUrgency) = ExtractIncidentDetails(rawMsg, msgLower);

                // Nếu ở chế độ Làm hộ và người dùng gõ "báo sự cố cho tôi" chung chung -> vẫn có thể tạo nhanh hoặc mở thẻ 1-Click
                if ((hasSpecificIssue || isExplicitActionMode) && currentSv != null && !string.IsNullOrWhiteSpace(activeMaPhong))
                {
                    if (!hasSpecificIssue && (msgLower == "báo cáo sự cố hộ tôi" || msgLower == "báo sự cố" || msgLower == "báo hỏng"))
                    {
                        // Mở bảng chọn nhanh 1-Click nếu câu quá ngắn chưa rõ thiết bị nào hỏng
                    }
                    else
                    {
                        string title = hasSpecificIssue ? extractedTitle : $"Báo cáo sự cố kỹ thuật tại phòng P.{activeMaPhong}";
                        string maSC = $"SC_{DateTime.Now:yyMMddHHmm}_{Random.Shared.Next(1000, 9999)}";
                        var bc = new BaoCaoSuCo
                        {
                            MaSuCo = maSC,
                            TenSuCo = title,
                            MoTa = $"[Tự động qua Trợ lý AI] {rawMsg}",
                            MucDoKhanCap = extractedUrgency,
                            HinhAnh = "",
                            MaSV = currentSv.MaSV,
                            MaPhong = activeMaPhong,
                            TrangThai = "Chờ xử lý",
                            NgayBaoCao = DateTime.Now
                        };

                        _context.BaoCaoSuCos.Add(bc);
                        await _context.SaveChangesAsync();

                        string autoMsg = $"✅ **Đã tự động lập phiếu báo cáo sự cố thành công!**\n" +
                                         $"Tôi đã thay mặt bạn ghi nhận sự cố **\"{title}\"** cho phòng **P.{activeMaPhong}** và chuyển thẳng tới Đội kỹ thuật KTX.";

                        return Json(new
                        {
                            response = autoMsg,
                            source = "Nhờ AI làm hộ (Đã thực thi tự động)",
                            actionCard = new
                            {
                                cardType = "RECEIPT_CARD",
                                receipt = new
                                {
                                    maPhieu = maSC,
                                    loaiPhieu = "Báo cáo sự cố kỹ thuật",
                                    maPhong = "P." + activeMaPhong,
                                    nguoiGui = $"{currentSv.MaSV} - {currentSv.HoTen}",
                                    noiDung = title,
                                    chiTiet = $"Mức độ: {extractedUrgency} | Nội dung: {rawMsg}",
                                    trangThai = "Chờ xử lý",
                                    thoiGian = bc.NgayBaoCao.ToString("dd/MM/yyyy HH:mm")
                                }
                            }
                        });
                    }
                }

                string replyMsg = "🛠 **Chọn nhanh thiết bị hỏng bên dưới (1-Click)** hoặc gõ trực tiếp tên thiết bị (VD: *\"Báo hỏng bóng đèn phòng tôi\"*) để tôi tự động lập phiếu ngay:";
                return Json(new
                {
                    response = replyMsg,
                    source = "Nhờ AI làm hộ (Báo cáo sự cố)",
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
            // D. TỰ ĐỘNG XIN CHUYỂN PHÒNG / ĐĂNG KÝ TRẢ PHÒNG
            // ---------------------------------------------------------------------
            if (actionIntent == "ACTION_CHUYEN_PHONG" || actionIntent == "ACTION_TRA_PHONG")
            {
                string defaultLoai = actionIntent == "ACTION_CHUYEN_PHONG" ? "Xin chuyển phòng" : "Đăng ký trả phòng";
                string sourceRoom = currentSv?.MaPhong ?? activeMaPhong;
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
                            targetRoom = mentionedRooms[0];
                        else
                            targetRoom = mentionedRooms[0];
                    }
                }

                // Tự động thực thi ngay lập tức cho Đăng ký trả phòng hoặc Chuyển phòng đã có phòng đích
                if (currentSv != null && (actionIntent == "ACTION_TRA_PHONG" || (!string.IsNullOrWhiteSpace(targetRoom) && !sourceRoom.Equals(targetRoom, StringComparison.OrdinalIgnoreCase))))
                {
                    string phongMongMuon;
                    string trangThaiDon = "Chờ Ban Quản lý duyệt";
                    string? phongCuDongBo = null;
                    string? phongMoiDongBo = null;

                    if (actionIntent == "ACTION_CHUYEN_PHONG")
                    {
                        var (phongDich, loiPhongDich) = await KiemTraPhongDichAsync(targetRoom, currentSv.MaSV);
                        if (phongDich == null)
                        {
                            return Json(new
                            {
                                response = $"⚠️ **Không thể tự động chuyển phòng:** {loiPhongDich}",
                                source = "Nhờ AI làm hộ (Kiểm tra quy định phòng)"
                            });
                        }
                        targetRoom = phongDich.MaPhong;
                        phongMongMuon = $"Từ phòng P.{sourceRoom} → Chuyển sang phòng P.{targetRoom}";
                        phongCuDongBo = currentSv.MaPhong ?? sourceRoom;
                        phongMoiDongBo = targetRoom;

                        currentSv.MaPhong = targetRoom;
                        var activeHd = await _context.HopDongs.FirstOrDefaultAsync(h => h.MaSV == currentSv.MaSV && h.TrangThai == "Còn hiệu lực");
                        if (activeHd != null)
                        {
                            activeHd.MaPhong = targetRoom;
                        }
                        trangThaiDon = "Đã chuyển phòng thành công";
                    }
                    else
                    {
                        phongMongMuon = $"Đăng ký trả phòng P.{sourceRoom}";
                    }

                    string maYC = $"YC_{DateTime.Now:yyMMddHHmm}_{Random.Shared.Next(1000, 9999)}";
                    var yc = new BaoCaoSuCo
                    {
                        MaSuCo = maYC,
                        TenSuCo = $"[{defaultLoai}] {phongMongMuon}",
                        MoTa = $"[Tự động qua Trợ lý AI] {rawMsg}",
                        MucDoKhanCap = "Bình thường",
                        HinhAnh = "",
                        MaSV = currentSv.MaSV,
                        MaPhong = currentSv.MaPhong ?? sourceRoom,
                        TrangThai = actionIntent == "ACTION_CHUYEN_PHONG" ? "Đã xử lý" : "Chờ xử lý",
                        NgayBaoCao = DateTime.Now
                    };

                    _context.BaoCaoSuCos.Add(yc);
                    await _context.SaveChangesAsync();

                    if (!string.IsNullOrEmpty(phongCuDongBo))
                        await DongBoTrangThaiPhongAsync(phongCuDongBo);
                    if (!string.IsNullOrEmpty(phongMoiDongBo))
                        await DongBoTrangThaiPhongAsync(phongMoiDongBo);

                    string doneMsg = actionIntent == "ACTION_CHUYEN_PHONG"
                        ? $"✅ **Đã tự động chuyển phòng thành công!**\nHồ sơ của bạn (**{currentSv.HoTen} - {currentSv.MaSV}**) đã được cập nhật từ phòng **P.{sourceRoom}** sang phòng **P.{targetRoom}**."
                        : $"✅ **Đã tự động lập đơn '{defaultLoai}' thành công!**\nTôi đã gửi hồ sơ trả phòng **P.{sourceRoom}** (Mã đơn: **{maYC}**) lên Ban Quản lý KTX.";

                    return Json(new
                    {
                        response = doneMsg,
                        source = "Nhờ AI làm hộ (Đã thực thi tự động)",
                        actionCard = new
                        {
                            cardType = "RECEIPT_CARD",
                            receipt = new
                            {
                                maPhieu = maYC,
                                loaiPhieu = defaultLoai,
                                maPhong = "P." + (currentSv.MaPhong ?? sourceRoom),
                                nguoiGui = $"{currentSv.MaSV} - {currentSv.HoTen}",
                                noiDung = phongMongMuon,
                                chiTiet = $"Yêu cầu: {rawMsg}",
                                trangThai = trangThaiDon,
                                thoiGian = yc.NgayBaoCao.ToString("dd/MM/yyyy HH:mm")
                            }
                        }
                    });
                }

                var availableRooms = allRooms
                    .Where(p => p.TinhTrang == "Còn chỗ" && !p.MaPhong.Equals(sourceRoom, StringComparison.OrdinalIgnoreCase))
                    .Select(p => new { maPhong = p.MaPhong, loaiPhong = p.LoaiPhong, giaPhong = p.GiaPhong })
                    .ToList();

                return Json(new
                {
                    response = $"🔄 **Chọn phòng muốn chuyển đến bên dưới** và bấm xác nhận (hoặc gõ trực tiếp kiểu *\"Chuyển phòng cho tôi sang phòng 102\"* để tôi tự động chuyển ngay):",
                    source = $"Nhờ AI làm hộ ({defaultLoai})",
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
            // E. TỰ ĐỘNG THANH TOÁN HÓA ĐƠN QUA CHAT
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

                // Lọc theo loại hóa đơn nếu người dùng chỉ định rõ ("tiền điện", "tiền phòng")
                if (msgLower.Contains("tiền điện") || msgLower.Contains("điện nước"))
                {
                    var filtered = chuaThanhToan.Where(h => h.LoaiHoaDon.Contains("Điện", StringComparison.OrdinalIgnoreCase)).ToList();
                    if (filtered.Any()) chuaThanhToan = filtered;
                }
                else if (msgLower.Contains("tiền phòng"))
                {
                    var filtered = chuaThanhToan.Where(h => h.LoaiHoaDon.Contains("phòng", StringComparison.OrdinalIgnoreCase)).ToList();
                    if (filtered.Any()) chuaThanhToan = filtered;
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
                    ? $"🧾 **Bạn hiện có {chuaThanhToan.Count} hóa đơn chưa thanh toán (Tổng nợ: {chuaThanhToan.Sum(x => x.TongTien):N0} VNĐ):**\n\n" +
                      $"🔒 **Quy định an toàn tài chính:** AI tuyệt đối KHÔNG tự ý thanh toán hay gạch nợ hóa đơn thay cho sinh viên.\n\n" +
                      $"👉 **Cách thức thanh toán an toàn:**\n" +
                      $"• **Sinh viên tự thanh toán:** Bấm nút *[Tự thanh toán]* bên dưới để chuyển sang trang **Hóa đơn của tôi** và nhập mật khẩu xác nhận.\n" +
                      $"• **Ban Quản lý thu:** Nộp tiền trực tiếp tại Văn phòng BQL để được Admin xác nhận gạch nợ trên hệ thống."
                    : "✅ Hiện tại tất cả hóa đơn của bạn đều **đã được thanh toán đầy đủ**! Dưới đây là biên lai các hóa đơn gần nhất:";

                return Json(new
                {
                    response = billMsg,
                    source = "Quản lý Hóa đơn KTX",
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
            // F. TỰ ĐỘNG YÊU CẦU CẤP PHÁT / BỔ SUNG CƠ SỞ VẬT CHẤT
            // ---------------------------------------------------------------------
            if (actionIntent == "ACTION_CO_SO_VAT_CHAT" && currentSv != null)
            {
                string maYC = "CSVC_" + DateTime.Now.ToString("yyMMddHHmmss");
                var yc = new BaoCaoSuCo
                {
                    MaSuCo = maYC,
                    TenSuCo = $"[Yêu cầu Cơ sở vật chất] P.{activeMaPhong}",
                    MoTa = $"[Tự động qua Trợ lý AI] {rawMsg}",
                    MucDoKhanCap = "Bình thường",
                    HinhAnh = "",
                    MaSV = currentSv.MaSV,
                    MaPhong = activeMaPhong,
                    TrangThai = "Chờ xử lý",
                    NgayBaoCao = DateTime.Now
                };
                _context.BaoCaoSuCos.Add(yc);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    response = $"✅ **Đã tự động gửi phiếu yêu cầu Cơ sở vật chất thành công!**\nYêu cầu của phòng **P.{activeMaPhong}** đã được ghi nhận vào hệ thống với mã **{maYC}**.",
                    source = "Nhờ AI làm hộ (Cơ sở vật chất)",
                    actionCard = new
                    {
                        cardType = "RECEIPT_CARD",
                        receipt = new
                        {
                            maPhieu = maYC,
                            loaiPhieu = "Yêu cầu trang thiết bị / CSVC",
                            maPhong = "P." + activeMaPhong,
                            nguoiGui = $"{currentSv.MaSV} - {currentSv.HoTen}",
                            noiDung = rawMsg,
                            chiTiet = "Bộ phận Quản trị thiết bị sẽ kiểm tra và cấp phát",
                            trangThai = "Chờ xử lý",
                            thoiGian = yc.NgayBaoCao.ToString("dd/MM/yyyy HH:mm")
                        }
                    }
                });
            }

            // ---------------------------------------------------------------------
            // G. TỰ ĐỘNG GỬI ĐƠN GIẢI TRÌNH / KHIẾU NẠI KỶ LUẬT
            // ---------------------------------------------------------------------
            if (actionIntent == "ACTION_KY_LUAT" && currentSv != null)
            {
                string maGT = "GT_" + DateTime.Now.ToString("yyMMddHHmmss");
                var yc = new BaoCaoSuCo
                {
                    MaSuCo = maGT,
                    TenSuCo = $"[Đơn giải trình kỷ luật] SV {currentSv.MaSV}",
                    MoTa = $"[Tự động qua Trợ lý AI] {rawMsg}",
                    MucDoKhanCap = "Bình thường",
                    HinhAnh = "",
                    MaSV = currentSv.MaSV,
                    MaPhong = activeMaPhong,
                    TrangThai = "Chờ xử lý",
                    NgayBaoCao = DateTime.Now
                };
                _context.BaoCaoSuCos.Add(yc);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    response = $"✅ **Đã tự động lập và gửi Đơn giải trình kỷ luật thành công!**\nĐơn mã **{maGT}** của sinh viên **{currentSv.HoTen} ({currentSv.MaSV})** đã được chuyển tới Ban Quản lý xem xét.",
                    source = "Nhờ AI làm hộ (Quản lý Kỷ luật)",
                    actionCard = new
                    {
                        cardType = "RECEIPT_CARD",
                        receipt = new
                        {
                            maPhieu = maGT,
                            loaiPhieu = "Đơn giải trình / Khiếu nại kỷ luật",
                            maPhong = "P." + activeMaPhong,
                            nguoiGui = $"{currentSv.MaSV} - {currentSv.HoTen}",
                            noiDung = rawMsg,
                            chiTiet = "Đã chuyển Ban Quản lý KTX",
                            trangThai = "Chờ duyệt",
                            thoiGian = yc.NgayBaoCao.ToString("dd/MM/yyyy HH:mm")
                        }
                    }
                });
            }

            // ---------------------------------------------------------------------
            // H. TỰ ĐỘNG CẬP NHẬT SỐ ĐIỆN THOẠI HỒ SƠ SINH VIÊN
            // ---------------------------------------------------------------------
            if (actionIntent == "ACTION_CAP_NHAT_SDT" && currentSv != null)
            {
                var phoneMatch = Regex.Match(rawMsg, @"0\d{9}");
                if (phoneMatch.Success)
                {
                    string oldPhone = currentSv.SoDienThoai;
                    currentSv.SoDienThoai = phoneMatch.Value;
                    await _context.SaveChangesAsync();

                    return Json(new
                    {
                        response = $"✅ **Đã tự động cập nhật số điện thoại mới cho {currentSv.HoTen} ({currentSv.MaSV})!**\n• SĐT cũ: {oldPhone} → **SĐT mới: {currentSv.SoDienThoai}**",
                        source = "Nhờ AI làm hộ (Cập nhật Hồ sơ)",
                        actionCard = new
                        {
                            cardType = "RECEIPT_CARD",
                            receipt = new
                            {
                                maPhieu = "HS_" + DateTime.Now.ToString("HHmmss"),
                                loaiPhieu = "Cập nhật Hồ sơ Sinh viên",
                                maPhong = "P." + activeMaPhong,
                                nguoiGui = $"{currentSv.MaSV} - {currentSv.HoTen}",
                                noiDung = $"Đổi SĐT: {oldPhone} → {currentSv.SoDienThoai}",
                                chiTiet = "Đã lưu trực tiếp vào Cơ sở dữ liệu",
                                trangThai = "Hoàn tất",
                                thoiGian = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
                            }
                        }
                    });
                }
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

        /// <summary>
        /// Kiểm tra phòng muốn chuyển đến: phải tồn tại, không ở trạng thái "Bảo trì" và còn giường trống
        /// (chỉ đếm sinh viên "Đang ở", không tính chính sinh viên đang chuyển). Trả về (Phòng hợp lệ, null) hoặc (null, Thông báo lỗi).
        /// </summary>
        private async Task<(Phong? Phong, string? Loi)> KiemTraPhongDichAsync(string maPhongDich, string maSVChuyen)
        {
            string ma = NormalizeRoomCode(maPhongDich);
            string maChuan = ma.Replace(".", "").Replace(" ", "").Replace("-", "");
            var phong = await _context.Phongs.FirstOrDefaultAsync(p => p.MaPhong.ToUpper() == ma);
            if (phong == null)
            {
                phong = (await _context.Phongs.ToListAsync())
                    .FirstOrDefault(p => p.MaPhong.Replace(".", "").Replace(" ", "").Replace("-", "").ToUpper() == maChuan);
            }
            if (phong == null)
                return (null, $"Phòng P.{ma} không tồn tại trong hệ thống KTX. Vui lòng chọn phòng khác!");

            if (phong.TinhTrang == "Bảo trì")
                return (null, $"Phòng P.{phong.MaPhong} đang bảo trì, tạm thời không tiếp nhận sinh viên. Vui lòng chọn phòng khác!");

            int soDangO = await _context.SinhViens.CountAsync(s => s.MaPhong == phong.MaPhong && s.TinhTrangLuuTru == "Đang ở" && s.MaSV != maSVChuyen);
            if (soDangO >= phong.SoLuongGiuong)
                return (null, $"Phòng P.{phong.MaPhong} đã đầy ({soDangO}/{phong.SoLuongGiuong} giường). Vui lòng chọn phòng còn chỗ trống!");

            return (phong, null);
        }

        /// <summary>
        /// Đồng bộ trạng thái phòng theo sĩ số thực tế (chỉ đếm sinh viên "Đang ở"):
        /// đủ giường -> "Đã đầy", còn giường -> "Còn chỗ". Không thay đổi phòng đang "Bảo trì".
        /// </summary>
        private async Task DongBoTrangThaiPhongAsync(string? maPhong)
        {
            if (string.IsNullOrEmpty(maPhong)) return;
            var phong = await _context.Phongs.FirstOrDefaultAsync(p => p.MaPhong == maPhong);
            if (phong == null) return;

            int soDangO = await _context.SinhViens.CountAsync(s => s.MaPhong == maPhong && s.TinhTrangLuuTru == "Đang ở");
            if (soDangO >= phong.SoLuongGiuong && phong.TinhTrang == "Còn chỗ")
            {
                phong.TinhTrang = "Đã đầy";
                await _context.SaveChangesAsync();
            }
            else if (soDangO < phong.SoLuongGiuong && phong.TinhTrang == "Đã đầy")
            {
                phong.TinhTrang = "Còn chỗ";
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Che số điện thoại của sinh viên khác khi hiển thị cho vai trò Sinh viên (bảo vệ quyền riêng tư). VD: 0981234567 -> 098****567
        /// </summary>
        private static string MaskPhone(string? phone)
        {
            return SafetyGuardrails.MaskPhone(phone);
        }

        private string? CleanAiOutput(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            return SafetyGuardrails.SanitizeHallucinations(SafetyGuardrails.ScrubPII(text));
        }
        private string ExtractJsonString(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "{}";
            var match = Regex.Match(input, @"\{.*\}", RegexOptions.Singleline);
            return match.Success ? match.Value : input;
        }

        /// <summary>
        /// Bộ trích xuất tri thức ngữ nghĩa thông minh (Semantic Knowledge Retriever) sử dụng TF-IDF Vector Space & Cosine Similarity.
        /// Đọc toàn bộ các facts trong knowledge_base.json và trích xuất Top 2-3 facts có điểm số tương đồng cao nhất với câu hỏi.
        /// </summary>
        public string RetrieveSemanticKnowledgeChunks(string question, int topK = 3, double threshold = 0.05)
        {
            try
            {
                string jsonPath = System.IO.Path.Combine(_env.WebRootPath, "data", "knowledge_base.json");
                if (!System.IO.File.Exists(jsonPath)) return string.Empty;

                var jsonContent = System.IO.File.ReadAllText(jsonPath);
                using var doc = JsonDocument.Parse(jsonContent);

                var allFacts = new List<string>();
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    // Lời chào không phải nội quy, bỏ qua để câu ngoài phạm vi không khớp nhầm vào đây
                    if (element.TryGetProperty("intent", out var intentEl) && intentEl.GetString() == "XIN_CHAO")
                        continue;
                    if (element.TryGetProperty("facts", out var factsEl) && factsEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var f in factsEl.EnumerateArray())
                        {
                            var str = f.GetString();
                            if (!string.IsNullOrWhiteSpace(str)) allFacts.Add(str.Trim());
                        }
                    }
                }

                if (!allFacts.Any()) return string.Empty;

                // Hàm tách từ (unigrams + bigrams)
                List<string> Tokenize(string text)
                {
                    string cleaned = Regex.Replace(text.ToLower(), @"[^\w\s]", " ");
                    var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
                    var tokens = new List<string>(words);
                    for (int i = 0; i < words.Count - 1; i++)
                    {
                        tokens.Add($"{words[i]}_{words[i + 1]}");
                    }
                    return tokens;
                }

                var docTokens = allFacts.Select(Tokenize).ToList();
                int n = allFacts.Count;

                var dfs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var tokens in docTokens)
                {
                    foreach (var t in tokens.Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        dfs[t] = dfs.GetValueOrDefault(t, 0) + 1;
                    }
                }

                double GetIdf(string term)
                {
                    int df = dfs.GetValueOrDefault(term, 0);
                    return Math.Log(1.0 + (double)n / (1.0 + df)) + 1.0;
                }

                var qTokens = Tokenize(question);
                if (!qTokens.Any()) return string.Empty;

                var qTf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var t in qTokens) qTf[t] = qTf.GetValueOrDefault(t, 0) + 1;

                var qVec = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in qTf)
                {
                    qVec[kvp.Key] = ((double)kvp.Value / qTokens.Count) * GetIdf(kvp.Key);
                }
                double qNorm = Math.Sqrt(qVec.Values.Sum(v => v * v));
                if (qNorm <= 0) return string.Empty;

                var scoredList = new List<(double Score, string Fact)>();

                for (int i = 0; i < allFacts.Count; i++)
                {
                    var dTokens = docTokens[i];
                    if (!dTokens.Any()) continue;

                    var dTf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    foreach (var t in dTokens) dTf[t] = dTf.GetValueOrDefault(t, 0) + 1;

                    var dVec = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kvp in dTf)
                    {
                        dVec[kvp.Key] = ((double)kvp.Value / dTokens.Count) * GetIdf(kvp.Key);
                    }
                    double dNorm = Math.Sqrt(dVec.Values.Sum(v => v * v));
                    if (dNorm <= 0) continue;

                    double dot = 0.0;
                    foreach (var kvp in qVec)
                    {
                        if (dVec.TryGetValue(kvp.Key, out double dVal))
                        {
                            dot += kvp.Value * dVal;
                        }
                    }

                    double sim = dot / (qNorm * dNorm);
                    if (sim >= threshold)
                    {
                        scoredList.Add((sim, allFacts[i]));
                    }
                }

                var topResults = scoredList
                    .OrderByDescending(x => x.Score)
                    .Take(topK)
                    .Select(x => x.Fact)
                    .ToList();

                if (topResults.Any())
                {
                    return "• " + string.Join("\n• ", topResults);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi trích xuất Semantic Retrieval từ knowledge_base.json");
            }

            return string.Empty;
        }

        private List<string> GetFactListByIntent(string intent)
        {
            try
            {
                string jsonPath = System.IO.Path.Combine(_env.WebRootPath, "data", "knowledge_base.json");
                if (!System.IO.File.Exists(jsonPath)) return new List<string>();

                var jsonContent = System.IO.File.ReadAllText(jsonPath);
                using var doc = JsonDocument.Parse(jsonContent);

                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    if (element.GetProperty("intent").GetString() == intent)
                    {
                        return element.GetProperty("facts").EnumerateArray()
                            .Select(f => f.GetString())
                            .Where(f => !string.IsNullOrWhiteSpace(f))
                            .Select(f => f!.Trim())
                            .ToList();
                    }
                }
            }
            catch { }

            return new List<string>();
        }

        private string GetFactsByIntent(string intent)
        {
            var facts = GetFactListByIntent(intent);
            if (facts.Any())
                return "• " + string.Join("\n• ", facts);

            return "Xin lỗi, mình chưa tìm thấy thông tin phù hợp. Bạn có thể hỏi về: 'giá phòng', 'phòng trống', 'giờ giới nghiêm', 'báo sự cố', 'điện nước', 'gửi xe', 'trả phòng', 'hợp đồng', 'kỷ luật', 'thống kê' hoặc nhờ mình thực hiện trực tiếp các thao tác nhé!";
        }

        private async Task<string?> CallOllamaOrNullAsync(string prompt, string? systemPrompt = null)
        {
            string baseUrl = _configuration["Ollama:BaseUrl"] ?? "http://localhost:11434/api/chat";
            string modelName = _configuration["Ollama:Model"] ?? "llama3";

            var payload = new
            {
                model = modelName,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt ?? AIPrompts.ChatbotSystemPrompt },
                    new { role = "user", content = prompt }
                },
                stream = false
            };

            try
            {
                // llama3 (8B) chạy trên máy cá nhân thường mất 5-20 giây, lần gọi đầu còn phải nạp model
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var httpClient = _httpClientFactory.CreateClient("Ollama");
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var res = await httpClient.PostAsync(baseUrl, content, cts.Token);
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    return doc.RootElement.GetProperty("message").GetProperty("content").GetString();
                }
                _logger.LogWarning("Ollama trả về mã {Status} cho model '{Model}' (kiểm tra đã chạy 'ollama pull {Model}' chưa) -> Sử dụng Fallback nội bộ.", (int)res.StatusCode, modelName, modelName);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Ollama ({Model}) không phản hồi trong 30 giây -> Sử dụng Fallback nội bộ.", modelName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Không kết nối được Ollama tại {Url} ({Msg}) -> Sử dụng Fallback nội bộ.", baseUrl, ex.Message);
            }

            return null;
        }
        private static bool IsOutOfDomain(string? input) => SafetyGuardrails.IsOutOfDomain(input);

    }

    public class ChatRequest
    {
        public string? Message { get; set; }
        public string? Mode { get; set; }
        public string? Role { get; set; }
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
        public string? MaSuCo { get; set; }
        public string? TrangThaiMoi { get; set; }
        public string? Role { get; set; }
    }
}
