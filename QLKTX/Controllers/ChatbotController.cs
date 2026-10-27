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
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLKTX.Models;

namespace QLKTX.Controllers
{
    public class ChatbotController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ChatbotController> _logger;

        public ChatbotController(IWebHostEnvironment env, ApplicationDbContext context,
            IHttpClientFactory httpClientFactory, ILogger<ChatbotController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _env = env;
            _context = context;
            _logger = logger;
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

        private async Task<IActionResult> AskInternal(ChatRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
                return Json(new { response = "Bạn cần mình hỗ trợ thông tin gì về KTX?" });

            string msgLower = request.Message.Trim().ToLower();

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
            else if (msgLower.Contains("báo hỏng") || msgLower.Contains("hướng dẫn báo") || msgLower.Contains("báo sự cố"))
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
                    string? aiRes = await CallOllamaOrNullAsync(prompt);
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
                        string? aiRes = await CallOllamaOrNullAsync(prompt);
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

                    var data = await _context.SinhViens.Where(s => s.TinhTrangLuuTru == "Đang ở").Take(20).AsNoTracking().ToListAsync();
                    var tongDangO = await _context.SinhViens.CountAsync(s => s.TinhTrangLuuTru == "Đang ở");
                    if (data != null && data.Any())
                    {
                        string prompt = $@"Dữ liệu mẫu Sinh viên đang ở trong KTX (Tổng: {tongDangO} SV):\n{JsonSerializer.Serialize(data)}\n\nTrả lời câu hỏi sau bằng tiếng Việt ngắn gọn:\n""{request.Message}""";
                        string? aiRes = await CallOllamaOrNullAsync(prompt);
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
                        string? aiRes = await CallOllamaOrNullAsync(prompt);
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
                        string? aiRes = await CallOllamaOrNullAsync(prompt);
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
                        string? aiRes = await CallOllamaOrNullAsync(prompt);
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
                        string? aiRes = await CallOllamaOrNullAsync(prompt);
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
            string finalPrompt = $@"Dữ liệu quy định KTX:\n{matchedFacts}\n\nTrả lời câu hỏi sinh viên bằng tiếng Việt lịch sự, dễ hiểu:\n""{request.Message}""";

            string? aiResponse = await CallOllamaOrNullAsync(finalPrompt);
            return Json(new { response = aiResponse ?? matchedFacts, source = "Cơ sở tri thức KTX" });
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

            return "Xin lỗi, mình chưa tìm thấy thông tin phù hợp. Bạn có thể hỏi về: 'giá phòng', 'phòng trống', 'giờ giới nghiêm', 'báo sự cố', 'điện nước', 'gửi xe', 'trả phòng', 'hợp đồng', 'kỷ luật' hoặc 'thống kê' nhé!";
        }

        private async Task<string?> CallOllamaOrNullAsync(string prompt)
        {
            var payload = new
            {
                model = "llama3",
                messages = new[]
                {
                    new { role = "system", content = "Bạn là Trợ lý AI Quản lý Ký túc xá. BẮT BUỘC trả lời 100% bằng TIẾNG VIỆT. Tuyệt đối CHỈ sử dụng dữ liệu được cung cấp trong câu hỏi để trả lời. Nếu dữ liệu không chứa câu trả lời, hãy nói 'Xin lỗi, tôi không có thông tin về vấn đề này trong hệ thống Ký túc xá'. KHÔNG ĐƯỢC tự bịa ra câu trả lời hoặc dùng kiến thức bên ngoài." },
                    new { role = "user", content = prompt }
                },
                stream = false
            };

            try
            {
                var httpClient = _httpClientFactory.CreateClient("Ollama");
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var res = await httpClient.PostAsync("http://localhost:11434/api/chat", content);
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
}
