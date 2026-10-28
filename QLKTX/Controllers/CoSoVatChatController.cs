using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QLKTX.Models;

namespace QLKTX.Controllers
{
    [AdminAuthorize(OnlyAdmin = true)]
    public class CoSoVatChatController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CoSoVatChatController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: CoSoVatChat/Index
        public async Task<IActionResult> Index(string? searchString, string? maPhong)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentMaPhong"] = maPhong;

            // Lấy danh sách tất cả mã phòng để đưa vào bộ lọc gợi ý / dropdown
            var danhSachPhong = await _context.Phongs
                .OrderBy(p => p.MaPhong)
                .Select(p => p.MaPhong)
                .Distinct()
                .ToListAsync();
            ViewBag.DanhSachPhong = danhSachPhong;

            // Lấy danh sách thiết bị và join với bảng Phòng
            var query = _context.CoSoVatChats.Include(c => c.Phong).AsQueryable();

            // 1. Lọc theo mã phòng cụ thể nếu được chọn từ dropdown hoặc bấm trực tiếp vào phòng
            if (!string.IsNullOrWhiteSpace(maPhong))
            {
                var roomFilter = maPhong.Trim();
                query = query.Where(c => c.MaPhong == roomFilter);
            }

            // 2. Lọc theo từ khóa tìm kiếm (hỗ trợ gõ mã phòng VD: 101, Phòng 101, P.101, tên thiết bị, mã TB...)
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var raw = searchString.Trim();

                // Chuẩn hóa tiền tố phòng nếu người dùng gõ dạng "Phòng 101", "phong 101", "P.101", "P101"
                var candidateRoom = raw;
                if (candidateRoom.StartsWith("Phòng ", StringComparison.OrdinalIgnoreCase))
                    candidateRoom = candidateRoom.Substring(6).Trim();
                else if (candidateRoom.StartsWith("Phong ", StringComparison.OrdinalIgnoreCase))
                    candidateRoom = candidateRoom.Substring(6).Trim();
                else if (candidateRoom.StartsWith("P.", StringComparison.OrdinalIgnoreCase))
                    candidateRoom = candidateRoom.Substring(2).Trim();
                else if (candidateRoom.StartsWith("P", StringComparison.OrdinalIgnoreCase) && candidateRoom.Length > 1 && char.IsDigit(candidateRoom[1]))
                    candidateRoom = candidateRoom.Substring(1).Trim();

                query = query.Where(c => c.MaPhong == candidateRoom
                                      || c.MaPhong.Contains(candidateRoom)
                                      || c.MaPhong.Contains(raw)
                                      || c.MaThietBi.Contains(raw)
                                      || c.TenThietBi.Contains(raw)
                                      || (c.LoaiThietBi != null && c.LoaiThietBi.Contains(raw))
                                      || (c.TinhTrang != null && c.TinhTrang.Contains(raw)));
            }

            var danhSachThietBi = await query.ToListAsync();
            return View(danhSachThietBi);
        }

        // GET: CoSoVatChat/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null) return NotFound();

            var coSoVatChat = await _context.CoSoVatChats
                .Include(c => c.Phong)
                .FirstOrDefaultAsync(m => m.MaThietBi == id);

            if (coSoVatChat == null) return NotFound();

            return View(coSoVatChat);
        }

        // GET: CoSoVatChat/Create
        public IActionResult Create()
        {
            ViewBag.MaPhong = new SelectList(_context.Phongs, "MaPhong", "MaPhong");
            return View();
        }

        // POST: CoSoVatChat/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CoSoVatChat coSoVatChat)
        {
            ModelState.Remove("Phong");
            ModelState.Remove("LoaiThietBi");
            coSoVatChat.LoaiThietBi ??= "";
            if (ModelState.IsValid)
            {
                if (_context.CoSoVatChats.Any(e => e.MaThietBi == coSoVatChat.MaThietBi))
                {
                    ModelState.AddModelError("MaThietBi", "Mã thiết bị đã tồn tại!");
                }
                else
                {
                    _context.Add(coSoVatChat);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
            }
            ViewBag.MaPhong = new SelectList(_context.Phongs, "MaPhong", "MaPhong", coSoVatChat.MaPhong);
            return View(coSoVatChat);
        }

        // GET: CoSoVatChat/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return NotFound();

            var coSoVatChat = await _context.CoSoVatChats.FindAsync(id);
            if (coSoVatChat == null) return NotFound();

            ViewBag.MaPhong = new SelectList(_context.Phongs, "MaPhong", "MaPhong", coSoVatChat.MaPhong);
            return View(coSoVatChat);
        }

        // POST: CoSoVatChat/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, CoSoVatChat coSoVatChat)
        {
            if (id != coSoVatChat.MaThietBi) return NotFound();

            ModelState.Remove("Phong");
            ModelState.Remove("LoaiThietBi");
            coSoVatChat.LoaiThietBi ??= "";

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(coSoVatChat);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.CoSoVatChats.Any(e => e.MaThietBi == coSoVatChat.MaThietBi)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewBag.MaPhong = new SelectList(_context.Phongs, "MaPhong", "MaPhong", coSoVatChat.MaPhong);
            return View(coSoVatChat);
        }

        // GET: CoSoVatChat/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null) return NotFound();

            var coSoVatChat = await _context.CoSoVatChats
                .Include(c => c.Phong)
                .FirstOrDefaultAsync(m => m.MaThietBi == id);

            if (coSoVatChat == null) return NotFound();

            return View(coSoVatChat);
        }

        // POST: CoSoVatChat/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var coSoVatChat = await _context.CoSoVatChats.FindAsync(id);
            if (coSoVatChat != null)
            {
                _context.CoSoVatChats.Remove(coSoVatChat);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
