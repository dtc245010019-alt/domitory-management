using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QLKTX.Models;

namespace QLKTX.Controllers
{
    public class CoSoVatChatController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CoSoVatChatController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: CoSoVatChat/Index
        public async Task<IActionResult> Index(string searchString)
        {
            ViewData["CurrentFilter"] = searchString;

            // Lấy danh sách thiết bị và join với bảng Phòng
            var query = _context.CoSoVatChats.Include(c => c.Phong).AsQueryable();

            // Lọc dữ liệu nếu có từ khóa
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(c => c.MaThietBi.Contains(searchString)
                                      || c.TenThietBi.Contains(searchString)
                                      || c.LoaiThietBi.Contains(searchString)
                                      || c.MaPhong.Contains(searchString)
                                      || c.TinhTrang.Contains(searchString));
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