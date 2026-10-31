using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QLKTX.Models;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace QLKTX.Controllers
{
    [AdminAuthorize]
    public class PhongController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public PhongController(ApplicationDbContext context, IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        // GET: Phong/Index
        public async Task<IActionResult> Index(string? searchString, string? toa, int? tang)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentToa"] = toa;
            ViewData["CurrentTang"] = tang;

            // Khởi tạo câu truy vấn (AsNoTracking tối ưu hóa hiệu năng nạp danh sách)
            var query = _context.Phongs.AsNoTracking().Include(p => p.SinhViens).AsQueryable();

            // Nếu có gõ từ khóa thì tiến hành lọc (chuẩn hóa tiền tố P. và dấu gạch ngang theo Luật 4)
            if (!string.IsNullOrEmpty(searchString))
            {
                string phongSearch = searchString.Trim().ToUpper();
                if (phongSearch.StartsWith("P.")) phongSearch = phongSearch.Substring(2);
                else if (phongSearch.StartsWith("P ") || phongSearch.StartsWith("P-")) phongSearch = phongSearch.Substring(2);
                phongSearch = phongSearch.Replace(".", "").Replace(" ", "").Replace("-", "");

                query = query.Where(p => p.MaPhong.Contains(searchString)
                                      || p.MaPhong.Replace(".", "").Replace(" ", "").Replace("-", "").ToUpper().Contains(phongSearch)
                                      || p.LoaiPhong.Contains(searchString)
                                      || p.TinhTrang.Contains(searchString)
                                      || p.Toa.Contains(searchString));
            }

            // Lọc theo Tòa
            if (!string.IsNullOrEmpty(toa))
            {
                query = query.Where(p => p.Toa == toa);
            }

            // Lọc theo Tầng
            if (tang.HasValue && tang.Value > 0)
            {
                query = query.Where(p => p.Tang == tang.Value);
            }

            // Lấy danh sách Tòa và Tầng cho bộ lọc dropdown
            ViewBag.ToaList = await _context.Phongs.Select(p => p.Toa).Distinct().OrderBy(t => t).ToListAsync();
            ViewBag.TangList = await _context.Phongs.Select(p => p.Tang).Distinct().OrderBy(t => t).ToListAsync();

            // Lấy dữ liệu đã lọc ra View
            var danhSachPhong = await query.ToListAsync();
            return View(danhSachPhong);
        }

        // GET: Phong/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null) return NotFound();

            var phong = await _context.Phongs
                .Include(p => p.SinhViens)
                .FirstOrDefaultAsync(m => m.MaPhong == id);

            if (phong == null) return NotFound();

            return View(phong);
        }

        // GET: Phong/Create
        [AdminAuthorize(OnlyAdmin = true)]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Phong/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize(OnlyAdmin = true)]
        public async Task<IActionResult> Create(Phong phong)
        {
            ModelState.Remove("SinhViens");
            ModelState.Remove("CoSoVatChats");
            ModelState.Remove("HopDongs");
            ModelState.Remove("TrangThaiThietBi");

            if (ModelState.IsValid)
            {
                if (_context.Phongs.Any(e => e.MaPhong == phong.MaPhong))
                {
                    ModelState.AddModelError("MaPhong", "Mã phòng đã tồn tại!");
                    return View(phong);
                }

                _context.Add(phong);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã thêm mới phòng {phong.MaPhong} thành công!";
                return RedirectToAction(nameof(Index));
            }
            return View(phong);
        }

        // GET: Phong/Edit/5
        [AdminAuthorize(OnlyAdmin = true)]
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return NotFound();

            var phong = await _context.Phongs.FindAsync(id);
            if (phong == null) return NotFound();

            return View(phong);
        }

        // POST: Phong/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize(OnlyAdmin = true)]
        public async Task<IActionResult> Edit(string id, Phong phong)
        {
            if (id != phong.MaPhong) return NotFound();

            // --- BỎ QUA KIỂM TRA CÁC LIST LIÊN KẾT ĐỂ TRÁNH LỖI ---
            ModelState.Remove("SinhViens");
            ModelState.Remove("CoSoVatChats");
            ModelState.Remove("HopDongs");
            ModelState.Remove("TrangThaiThietBi");
            // ----------------------------------------------------------

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(phong);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Phongs.Any(e => e.MaPhong == phong.MaPhong)) return NotFound();
                    else throw;
                }
                TempData["SuccessMessage"] = $"Đã cập nhật thông tin phòng {phong.MaPhong}!";
                return RedirectToAction(nameof(Index));
            }
            return View(phong);
        }

        // GET: Phong/Delete/5
        [AdminAuthorize(OnlyAdmin = true)]
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null) return NotFound();

            var phong = await _context.Phongs
                .FirstOrDefaultAsync(m => m.MaPhong == id);

            if (phong == null) return NotFound();

            return View(phong);
        }

        // POST: Phong/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [AdminAuthorize(OnlyAdmin = true)]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var phong = await _context.Phongs.FindAsync(id);
            if (phong != null)
            {
                bool hasStudent = await _context.SinhViens.AnyAsync(s => s.MaPhong == id);
                bool hasContract = await _context.HopDongs.AnyAsync(h => h.MaPhong == id);
                bool hasInvoice = await _context.HoaDons.AnyAsync(h => h.MaPhong == id);
                bool hasIncident = await _context.BaoCaoSuCos.AnyAsync(b => b.MaPhong == id);

                if (hasStudent || hasContract || hasInvoice || hasIncident)
                {
                    TempData["ErrorMessage"] = $"Không thể xóa cứng phòng {id} vì đang có sinh viên hoặc lịch sử lưu trú/hóa đơn liên kết! Bạn có thể đổi trạng thái phòng sang 'Bảo trì'.";
                    return RedirectToAction(nameof(Index));
                }

                var csvc = await _context.CoSoVatChats.Where(c => c.MaPhong == id).ToListAsync();
                if (csvc.Any())
                {
                    _context.CoSoVatChats.RemoveRange(csvc);
                }

                _context.Phongs.Remove(phong);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã xóa phòng {id} thành công!";
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Phong/GoiYXepPhongAI
        [HttpPost]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<IActionResult> GoiYXepPhongAI([FromBody] GoiYPhongRequest req)
        {
            if (SafetyGuardrails.IsPromptInjection(req?.LoaiPhong, out _) || SafetyGuardrails.IsPromptInjection(req?.TangOrToa, out _))
            {
                return Json(new { success = false, suggestion = "Yêu cầu xếp phòng vi phạm rào chắn an toàn hệ thống (Safety Guardrails)." });
            }
            try
            {
                var phongs = await _context.Phongs
                    .Include(p => p.SinhViens)
                    .AsNoTracking()
                    .ToListAsync();

                var danhSachConCho = phongs
                    .Select(p =>
                    {
                        int soDangO = p.SinhViens?.Count(s => s.TinhTrangLuuTru == "Đang ở") ?? 0;
                        return new
                        {
                            p.MaPhong,
                            p.LoaiPhong,
                            p.SoLuongGiuong,
                            p.Toa,
                            p.Tang,
                            SoDangO = soDangO,
                            GiuongTrong = p.SoLuongGiuong - soDangO,
                            p.GiaPhong,
                            p.TinhTrang
                        };
                    })
                    .Where(p => p.TinhTrang == "Còn chỗ" && p.GiuongTrong > 0)
                    .ToList();

                if (!string.IsNullOrWhiteSpace(req?.LoaiPhong))
                {
                    danhSachConCho = danhSachConCho
                        .Where(p => p.LoaiPhong.Contains(req.LoaiPhong, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                // Lọc chính xác theo trường Toa và Tang
                if (!string.IsNullOrWhiteSpace(req?.Toa))
                {
                    danhSachConCho = danhSachConCho
                        .Where(p => string.Equals(p.Toa, req.Toa, StringComparison.OrdinalIgnoreCase) || p.Toa.Contains(req.Toa, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                if (req?.Tang > 0)
                {
                    danhSachConCho = danhSachConCho
                        .Where(p => p.Tang == req.Tang.Value)
                        .ToList();
                }

                if (!string.IsNullOrWhiteSpace(req?.TangOrToa))
                {
                    if (int.TryParse(req.TangOrToa, out int tVal))
                    {
                        danhSachConCho = danhSachConCho
                            .Where(p => p.Tang == tVal)
                            .ToList();
                    }
                    else
                    {
                        danhSachConCho = danhSachConCho
                            .Where(p => p.Toa.Contains(req.TangOrToa, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                    }
                }

                if (req?.SoGiuongCan > 0)
                {
                    danhSachConCho = danhSachConCho
                        .Where(p => p.GiuongTrong >= req.SoGiuongCan)
                        .ToList();
                }

                var topPhongs = danhSachConCho
                    .OrderByDescending(p => p.GiuongTrong)
                    .ThenBy(p => p.GiaPhong)
                    .Take(5)
                    .ToList();

                if (!topPhongs.Any())
                {
                    return Json(new
                    {
                        success = true,
                        suggestion = "Hiện không tìm thấy phòng nào còn đủ giường trống khớp với tiêu chí bạn vừa chọn. Vui lòng thử chọn tầng khác hoặc tất cả loại phòng."
                    });
                }

                string criteriaDesc = $"Loại phòng: {(string.IsNullOrWhiteSpace(req?.LoaiPhong) ? "Tất cả" : req.LoaiPhong)}, " +
                                      $"Khu/Tầng: {(string.IsNullOrWhiteSpace(req?.TangOrToa) ? "Tất cả" : req.TangOrToa)}, " +
                                      $"Số chỗ cần xếp: {(req?.SoGiuongCan > 0 ? req.SoGiuongCan : 1)} sinh viên.";

                string roomsJson = JsonSerializer.Serialize(topPhongs);
                string prompt = AIPrompts.BuildRoomRecommendationPrompt(criteriaDesc, roomsJson);

                string baseUrl = _configuration["Ollama:BaseUrl"] ?? "http://localhost:11434/api/chat";
                string modelName = _configuration["Ollama:Model"] ?? "llama3";

                var recommendedList = topPhongs.Take(3).Select(p => new
                {
                    maPhong = p.MaPhong,
                    loaiPhong = p.LoaiPhong,
                    toa = p.Toa,
                    tang = p.Tang,
                    giuongTrong = p.GiuongTrong,
                    soLuongGiuong = p.SoLuongGiuong,
                    giaPhong = p.GiaPhong
                }).ToList();

                try
                {
                    var httpClient = _httpClientFactory.CreateClient("Ollama");
                    httpClient.Timeout = TimeSpan.FromSeconds(45); // llama3 cần nhiều thời gian hơn để phân tích danh sách phòng
                    var payload = new
                    {
                        model = modelName,
                        messages = new[]
                        {
                            new { role = "system", content = AIPrompts.RoomRecommendationSystemPrompt },
                            new { role = "user", content = prompt }
                        },
                        stream = false
                    };
                    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                    var res = await httpClient.PostAsync(baseUrl, content);
                    if (res.IsSuccessStatusCode)
                    {
                        var json = await res.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        var aiText = doc.RootElement.GetProperty("message").GetProperty("content").GetString();
                        if (!string.IsNullOrWhiteSpace(aiText))
                        {
                            return Json(new
                            {
                                success = true,
                                suggestion = SafetyGuardrails.SanitizeHallucinations(SafetyGuardrails.ScrubPII(aiText)),
                                recommendedMaPhong = topPhongs.First().MaPhong,
                                recommendedRooms = recommendedList
                            });
                        }
                    }
                }
                catch { }

                // Fallback gợi ý thông minh khi dịch vụ Ollama chưa bật
                var sb = new StringBuilder();
                sb.AppendLine($"✨ AI ĐỀ XUẤT XẾP PHÒNG TỐI ƯU ({criteriaDesc}):");
                sb.AppendLine();
                int idx = 1;
                foreach (var p in topPhongs.Take(3))
                {
                    sb.AppendLine($"{idx}. Phòng P.{p.MaPhong} ({p.LoaiPhong}):");
                    sb.AppendLine($"   • Chỗ trống: Còn {p.GiuongTrong}/{p.SoLuongGiuong} giường trống (Đang ở: {p.SoDangO} SV)");
                    sb.AppendLine($"   • Mức phí: {p.GiaPhong:N0} VNĐ/tháng/SV (hoặc {(p.GiaPhong * 5):N0} VNĐ/kỳ 5 tháng)");
                    sb.AppendLine($"   • Lý do đề xuất: Phòng thông thoáng, mật độ lưu trú thấp ({p.SoDangO}/{p.SoLuongGiuong}), sẵn sàng nhận ngay {req?.SoGiuongCan ?? 1} sinh viên.");
                    idx++;
                }
                return Json(new
                {
                    success = true,
                    suggestion = sb.ToString(),
                    recommendedMaPhong = topPhongs.First().MaPhong,
                    recommendedRooms = recommendedList
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, suggestion = "Lỗi phân tích gợi ý phòng: " + ex.Message });
            }
        }
    }

    public class GoiYPhongRequest
    {
        public string? LoaiPhong { get; set; }
        public string? TangOrToa { get; set; }
        public string? Toa { get; set; }
        public int? Tang { get; set; }
        public int SoGiuongCan { get; set; } = 1;
    }
}
