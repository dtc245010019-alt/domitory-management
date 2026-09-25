using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QLKTX.Models;

namespace QLKTX.Controllers
{
    public class HopDongController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HopDongController(ApplicationDbContext context)
        {
            _context = context;
        }


        // GET: HopDong/Index
        public async Task<IActionResult> Index(string searchString)
        {
            // Giữ lại từ khóa trên ô tìm kiếm
            ViewData["CurrentFilter"] = searchString;

            // Kết nối các bảng để lấy tên sinh viên và mã phòng
            var query = _context.HopDongs
                .Include(h => h.SinhVien)
                .Include(h => h.Phong)
                .AsQueryable();

            // Lọc dữ liệu nếu có từ khóa (Tìm cả Mã HĐ, Mã SV, Tên SV, Mã Phòng, Trạng thái)
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(h => h.MaHopDong.Contains(searchString)
                                      || h.MaSV.Contains(searchString)
                                      || (h.SinhVien != null && h.SinhVien.HoTen.Contains(searchString))
                                      || h.MaPhong.Contains(searchString)
                                      || h.TrangThai.Contains(searchString));
            }

            var danhSachHopDong = await query.ToListAsync();
            return View(danhSachHopDong);
        }

        // GET: HopDong/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null) return NotFound();

            var hopDong = await _context.HopDongs
                .Include(h => h.SinhVien)
                .Include(h => h.Phong)
                .FirstOrDefaultAsync(m => m.MaHopDong == id);

            if (hopDong == null) return NotFound();

            return View(hopDong);
        }

        // GET: HopDong/Create
        public IActionResult Create()
        {
            // Đổ dữ liệu vào Dropdown list cho Sinh viên (hiển thị Họ tên) và Phòng
            ViewBag.MaSV = new SelectList(_context.SinhViens, "MaSV", "HoTen");
            ViewBag.MaPhong = new SelectList(_context.Phongs, "MaPhong", "MaPhong");
            return View();
        }

        // POST: HopDong/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(HopDong hopDong)
        {
            ModelState.Remove("SinhVien");
            ModelState.Remove("Phong");

            if (ModelState.IsValid)
            {
                if (_context.HopDongs.Any(e => e.MaHopDong == hopDong.MaHopDong))
                {
                    ModelState.AddModelError("MaHopDong", "Mã hợp đồng đã tồn tại!");
                }
                else
                {
                    _context.Add(hopDong);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
            }
            ViewBag.MaSV = new SelectList(_context.SinhViens, "MaSV", "HoTen", hopDong.MaSV);
            ViewBag.MaPhong = new SelectList(_context.Phongs, "MaPhong", "MaPhong", hopDong.MaPhong);
            return View(hopDong);
        }

        // GET: HopDong/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return NotFound();

            var hopDong = await _context.HopDongs.FindAsync(id);
            if (hopDong == null) return NotFound();

            ViewBag.MaSV = new SelectList(_context.SinhViens, "MaSV", "HoTen", hopDong.MaSV);
            ViewBag.MaPhong = new SelectList(_context.Phongs, "MaPhong", "MaPhong", hopDong.MaPhong);
            return View(hopDong);
        }

        // POST: HopDong/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, HopDong hopDong)
        {
            if (id != hopDong.MaHopDong) return NotFound();
            ModelState.Remove("SinhVien");
            ModelState.Remove("Phong");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(hopDong);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.HopDongs.Any(e => e.MaHopDong == hopDong.MaHopDong)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewBag.MaSV = new SelectList(_context.SinhViens, "MaSV", "HoTen", hopDong.MaSV);
            ViewBag.MaPhong = new SelectList(_context.Phongs, "MaPhong", "MaPhong", hopDong.MaPhong);
            return View(hopDong);
        }

        // GET: HopDong/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null) return NotFound();

            var hopDong = await _context.HopDongs
                .Include(h => h.SinhVien)
                .Include(h => h.Phong)
                .FirstOrDefaultAsync(m => m.MaHopDong == id);

            if (hopDong == null) return NotFound();

            return View(hopDong);
        }

        // POST: HopDong/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var hopDong = await _context.HopDongs.FindAsync(id);
            if (hopDong != null)
            {
                _context.HopDongs.Remove(hopDong);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}