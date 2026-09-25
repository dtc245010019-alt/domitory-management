using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
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
            // Bọc toàn bộ logic trong try/catch để ĐẢM BẢO luôn trả về JSON hợp lệ,
            // không bao giờ để lộ trang lỗi HTML ra cho fetch() ở frontend
            // (đây chính là nguyên nhân gây bong bóng đỏ "Không thể kết nối AI Chatbot!").
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

            // 1. Phân tích Intent bằng AI
            string classifyPrompt = $@"Phân tích câu hỏi và trả về duy nhất định dạng JSON:
{{
  ""intent"": ""PHONG_TRONG"" | ""HOADON_DIEN_NUOC"" | ""BAO_HONG_THIET_BI"" | ""GIOGIAC_NOIQUY"" | ""GUI_XE_RA_VAO"" | ""DINH_NGHIA"" | ""KHAC"",
  ""keyword"": ""từ khóa nếu hỏi khái niệm, ngược lại để rỗng""
}}

Ví dụ câu hỏi phòng trống: ""Còn phòng nào trống không"", ""Có phòng trống không?"" -> intent: ""PHONG_TRONG""

Câu hỏi: ""{request.Message}""";

            string jsonIntentResponse = await CallOllamaAsync(classifyPrompt);
            string intent = "KHAC";
            string keyword = "";

            try
            {
                using var doc = JsonDocument.Parse(jsonIntentResponse);
                if (doc.RootElement.TryGetProperty("intent", out var intentElem))
                    intent = intentElem.GetString() ?? "KHAC";
                if (doc.RootElement.TryGetProperty("keyword", out var keyElem))
                    keyword = keyElem.GetString() ?? "";
            }
            catch { }

            // 2. Tra cứu PHÒNG TRỐNG trực tiếp từ Database
            if (intent == "PHONG_TRONG")
            {
                try
                {
                    var dsPhong = await _context.Phongs.ToListAsync();
                    if (dsPhong != null && dsPhong.Any())
                    {
                        // Sử dụng p.MaPhong đúng theo CSDL
                        string danhSachStr = string.Join("\n", dsPhong.Select(p => $"- Phòng {p.MaPhong} ({p.LoaiPhong}): {p.TinhTrang}"));
                        return Json(new { 
                            response = $"Thông tin danh sách phòng hiện tại:\n{danhSachStr}\n\nBạn hãy đến trực tiếp Ban quản lý KTX để làm thủ tục đăng ký nhé!",
                            source = "Cơ sở dữ liệu KTX"
                        });
                    }
                }
                catch { }
            }

            // 3. Tra cứu Wikipedia nếu hỏi Khái niệm
            if (intent == "DINH_NGHIA" && !string.IsNullOrWhiteSpace(keyword))
            {
                string wikiSummary = await GetWikipediaSummaryAsync(keyword);
                if (!string.IsNullOrEmpty(wikiSummary))
                {
                    string wikiPrompt = $@"Giải thích ngắn gọn khái niệm dựa trên thông tin:
Tóm tắt: ""{wikiSummary}""
Câu hỏi: ""{request.Message}""";
                    string wikiResponse = await CallOllamaAsync(wikiPrompt);
                    return Json(new { response = wikiResponse, source = "Wikipedia" });
                }
            }

            // 4. Tra cứu quy định KTX từ file JSON
            string matchedFacts = GetFactsByIntent(intent);
            string finalPrompt = $@"Bạn là Trợ lý AI KTX.
Dữ liệu quy định:
{matchedFacts}

Trả lời câu hỏi sinh viên ngắn gọn, lịch sự:
""{request.Message}""";

            string aiResponse = await CallOllamaAsync(finalPrompt);
            return Json(new { response = aiResponse, source = "Hệ thống KTX" });
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

            return "Xin lỗi, mình chưa tìm thấy thông tin phù hợp. Bạn có thể hỏi về: 'giờ giấc', 'hóa đơn', 'báo cáo sự cố' nhé!";
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
                model = "qwen2.5:3b",
                messages = new[] { new { role = "user", content = prompt } },
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
                _logger.LogWarning("Ollama trả về mã lỗi {StatusCode}", res.StatusCode);
            }
            catch (Exception ex)
            {
                // Log rõ lý do thật (Ollama chưa chạy, sai port, timeout, model chưa pull, v.v.)
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