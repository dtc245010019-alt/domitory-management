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

            string msgLower = request.Message.ToLower();

            // 1. Phân tích Intent bằng AI
            string classifyPrompt = $@"Phân tích câu hỏi và trả về duy nhất định dạng JSON thuần túy không kèm giải thích:
{{
  ""intent"": ""SINH_VIEN"" | ""PHONG"" | ""HOP_DONG"" | ""CO_SO_VAT_CHAT"" | ""KY_LUAT"" | ""THONG_KE"" | ""DINH_NGHIA"" | ""KHAC"",
  ""keyword"": ""từ khóa nếu hỏi khái niệm, ngược lại để rỗng""
}}

Câu hỏi: ""{request.Message}""";

            string jsonIntentResponse = await CallOllamaAsync(classifyPrompt);
            string intent = "KHAC";
            string keyword = "";

            try
            {
                string cleanJson = ExtractJsonString(jsonIntentResponse);
                using var doc = JsonDocument.Parse(cleanJson);
                if (doc.RootElement.TryGetProperty("intent", out var intentElem))
                    intent = intentElem.GetString() ?? "KHAC";
                if (doc.RootElement.TryGetProperty("keyword", out var keyElem))
                    keyword = keyElem.GetString() ?? "";
            }
            catch { }

            // 1.1. Cơ chế fallback từ khóa nhận diện các menu hệ thống
            if (intent == "KHAC")
            {
                if (msgLower.Contains("thống kê") || msgLower.Contains("báo cáo") || msgLower.Contains("tổng số") || msgLower.Contains("bao nhiêu sinh viên") || msgLower.Contains("bao nhiêu phòng"))
                    intent = "THONG_KE";
                else if (msgLower.Contains("sinh viên") || msgLower.Contains("người") || msgLower.Contains("tên") || msgLower.Contains("thành viên") || msgLower.Contains("mã sv") || msgLower.Contains("ở phòng"))
                    intent = "SINH_VIEN";
                else if (msgLower.Contains("phòng") || msgLower.Contains("chỗ trống") || msgLower.Contains("loại phòng") || msgLower.Contains("giá phòng"))
                    intent = "PHONG";
                else if (msgLower.Contains("hợp đồng") || msgLower.Contains("đăng ký") || msgLower.Contains("ngày hết hạn") || msgLower.Contains("gia hạn") || msgLower.Contains("thời hạn"))
                    intent = "HOP_DONG";
                else if (msgLower.Contains("thiết bị") || msgLower.Contains("cơ sở vật chất") || msgLower.Contains("điều hòa") || msgLower.Contains("quạt") || msgLower.Contains("đèn") || msgLower.Contains("sửa") || msgLower.Contains("hỏng"))
                    intent = "CO_SO_VAT_CHAT";
                else if (msgLower.Contains("vi phạm") || msgLower.Contains("kỷ luật") || msgLower.Contains("phạt") || msgLower.Contains("lỗi") || msgLower.Contains("ghi chú"))
                    intent = "KY_LUAT";
            }

            // 2. Tra cứu THỐNG KÊ TỔNG QUAN
            if (intent == "THONG_KE")
            {
                try
                {
                    var tongSV = await _context.SinhViens.CountAsync();
                    var tongPhong = await _context.Phongs.CountAsync();
                    var tongHopDong = await _context.HopDongs.CountAsync();
                    var tongThietBi = await _context.CoSoVatChats.CountAsync();
                    var tongViPham = await _context.ViPhams.CountAsync();

                    var thongKeData = new
                    {
                        TongSoSinhVien = tongSV,
                        TongSoPhong = tongPhong,
                        TongSoHopDong = tongHopDong,
                        TongSoThietBi = tongThietBi,
                        TongSoTruongHopViPham = tongViPham
                    };

                    string prompt = $@"Dưới đây là số liệu thống kê tổng quan của KTX từ CSDL:\n{JsonSerializer.Serialize(thongKeData)}\n\nDựa vào dữ liệu trên, hãy trả lời câu hỏi bằng tiếng Việt rõ ràng, chính xác:\n""{request.Message}""";
                    string response = await CallOllamaAsync(prompt);
                    return Json(new { response = response, source = "Thống kê Hệ thống KTX" });
                }
                catch (Exception ex) { _logger.LogError(ex, "Lỗi Thống kê"); }
            }

            // 3. Tra cứu SINH VIÊN
            if (intent == "SINH_VIEN")
            {
                try
                {
                    var data = await _context.SinhViens.ToListAsync();
                    if (data != null && data.Any())
                    {
                        string prompt = $@"Dữ liệu Danh sách Sinh viên trong KTX:\n{JsonSerializer.Serialize(data)}\n\nTrả lời câu hỏi sau bằng tiếng Việt ngắn gọn, liệt kê cụ thể nếu có:\n""{request.Message}""";
                        string response = await CallOllamaAsync(prompt);
                        return Json(new { response = response, source = "Quản lý Sinh viên" });
                    }
                }
                catch (Exception ex) { _logger.LogError(ex, "Lỗi CSDL Sinh viên"); }
            }

            // 4. Tra cứu PHÒNG
            if (intent == "PHONG")
            {
                try
                {
                    var data = await _context.Phongs.ToListAsync();
                    if (data != null && data.Any())
                    {
                        string prompt = $@"Dữ liệu Danh sách Phòng trong KTX:\n{JsonSerializer.Serialize(data)}\n\nTrả lời câu hỏi sau bằng tiếng Việt chính xác, rõ ràng:\n""{request.Message}""";
                        string response = await CallOllamaAsync(prompt);
                        return Json(new { response = response, source = "Quản lý Phòng" });
                    }
                }
                catch (Exception ex) { _logger.LogError(ex, "Lỗi CSDL Phòng"); }
            }

            // 5. Tra cứu HỢP ĐỒNG
            if (intent == "HOP_DONG")
            {
                try
                {
                    var data = await _context.HopDongs.ToListAsync();
                    if (data != null && data.Any())
                    {
                        string prompt = $@"Dữ liệu Hợp đồng KTX:\n{JsonSerializer.Serialize(data)}\n\nTrả lời câu hỏi của người dùng bằng tiếng Việt ngắn gọn:\n""{request.Message}""";
                        string response = await CallOllamaAsync(prompt);
                        return Json(new { response = response, source = "Quản lý Hợp đồng" });
                    }
                }
                catch (Exception ex) { _logger.LogError(ex, "Lỗi CSDL Hợp đồng"); }
            }

            // 6. Tra cứu CƠ SỞ VẬT CHẤT
            if (intent == "CO_SO_VAT_CHAT")
            {
                try
                {
                    var data = await _context.CoSoVatChats.ToListAsync();
                    if (data != null && data.Any())
                    {
                        string prompt = $@"Dữ liệu Cơ sở vật chất / Thiết bị KTX:\n{JsonSerializer.Serialize(data)}\n\nTrả lời câu hỏi bằng tiếng Việt chính xác theo tình trạng thiết bị:\n""{request.Message}""";
                        string response = await CallOllamaAsync(prompt);
                        return Json(new { response = response, source = "Quản lý Cơ sở vật chất" });
                    }
                }
                catch (Exception ex) { _logger.LogError(ex, "Lỗi CSDL Cơ sở vật chất"); }
            }

            // 7. Tra cứu KỶ LUẬT / VI PHẠM
            if (intent == "KY_LUAT")
            {
                try
                {
                    var data = await _context.ViPhams.ToListAsync();
                    if (data != null && data.Any())
                    {
                        string prompt = $@"Dữ liệu Sinh viên Vi phạm Kỷ luật KTX:\n{JsonSerializer.Serialize(data)}\n\nTrả lời câu hỏi bằng tiếng Việt rõ ràng, ngắn gọn:\n""{request.Message}""";
                        string response = await CallOllamaAsync(prompt);
                        return Json(new { response = response, source = "Quản lý Kỷ luật" });
                    }
                }
                catch (Exception ex) { _logger.LogError(ex, "Lỗi CSDL Kỷ luật"); }
            }

            // 8. Tra cứu Wikipedia (Định nghĩa)
            if (intent == "DINH_NGHIA" && !string.IsNullOrWhiteSpace(keyword))
            {
                string wikiSummary = await GetWikipediaSummaryAsync(keyword);
                if (!string.IsNullOrEmpty(wikiSummary))
                {
                    string wikiPrompt = $@"Giải thích ngắn gọn khái niệm bằng tiếng Việt dựa trên thông tin: Tóm tắt: ""{wikiSummary}"" - Câu hỏi: ""{request.Message}""";
                    string wikiResponse = await CallOllamaAsync(wikiPrompt);
                    return Json(new { response = wikiResponse, source = "Wikipedia" });
                }
            }

            // 9. Tra cứu Quy định chung từ file JSON Knowledge Base
            string matchedFacts = GetFactsByIntent(intent);
            string finalPrompt = $@"Dữ liệu quy định KTX:\n{matchedFacts}\n\nTrả lời câu hỏi sinh viên bằng tiếng Việt lịch sự, dễ hiểu:\n""{request.Message}""";

            string aiResponse = await CallOllamaAsync(finalPrompt);
            return Json(new { response = aiResponse, source = "Hệ thống KTX" });
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
                        return string.Join("\n- ", factsList.Select(f => f.GetString()));
                    }
                }
            }
            catch { }

            return "Xin lỗi, mình chưa tìm thấy thông tin phù hợp. Bạn có thể hỏi về: 'sinh viên', 'phòng', 'hợp đồng', 'cơ sở vật chất', 'kỷ luật' hoặc 'thống kê' nhé!";
        }

        private async Task<string> GetWikipediaSummaryAsync(string keyword)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("Ollama");
                var req = new HttpRequestMessage(HttpMethod.Get, $"https://vi.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(keyword)}");
                req.Headers.Add("User-Agent", "QLKTX-Chatbot/1.0");
                var res = await httpClient.SendAsync(req);
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("extract", out var ext))
                        return ext.GetString();
                }
            }
            catch { }
            return null;
        }

        private async Task<string> CallOllamaAsync(string prompt)
        {
            var payload = new
            {
                model = "llama3",
                messages = new[]
                {
                    new { role = "system", content = "Bạn là Trợ lý AI Quản lý Ký túc xá. BẮT BUỘC luôn luôn trả lời 100% bằng TIẾNG VIỆT. Tuyệt đối KHÔNG trả lời bằng tiếng Anh." },
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
                _logger.LogError(ex, "Không gọi được Ollama tại localhost:11434");
            }

            return "Không thể kết nối dịch vụ AI. (Kiểm tra Ollama đã chạy chưa?)";
        }
    }

    public class ChatRequest
    {
        public string Message { get; set; }
    }
}