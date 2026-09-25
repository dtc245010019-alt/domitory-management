using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLKTX.Models;

namespace QLKTX.Controllers
{
    public class PhongController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PhongController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Phong/Index
        public async Task<IActionResult> Index(string searchString)
        {
            // Giữ lại từ khóa trên ô tìm kiếm sau khi enter
            ViewData["CurrentFilter"] = searchString;

            // Khởi tạo câu truy vấn
            var query = _context.Phongs.AsQueryable();

            // Nếu có gõ từ khóa thì tiến hành lọc
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(p => p.MaPhong.Contains(searchString)
                                      || p.LoaiPhong.Contains(searchString)
                                      || p.TinhTrang.Contains(searchString));
            }

            // Lấy dữ liệu đã lọc ra View
            var danhSachPhong = await query.ToListAsync();
            return View(danhSachPhong);
        }

        // GET: Phong/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null) return NotFound();

            var phong = await _context.Phongs
                .FirstOrDefaultAsync(m => m.MaPhong == id);

            if (phong == null) return NotFound();

            return View(phong);
        }

        // GET: Phong/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Phong/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Phong phong)
        {
            if (ModelState.IsValid)
            {
                if (_context.Phongs.Any(e => e.MaPhong == phong.MaPhong))
                {
                    ModelState.AddModelError("MaPhong", "Mã phòng đã tồn tại!");
                    return View(phong);
                }

                _context.Add(phong);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(phong);
        }

        // GET: Phong/Edit/5
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
                return RedirectToAction(nameof(Index));
            }
            return View(phong);
        }

        // GET: Phong/Delete/5
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
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var phong = await _context.Phongs.FindAsync(id);
            if (phong != null)
            {
                _context.Phongs.Remove(phong);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}