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
            var query = _context.Phongs.Include(p => p.SinhViens).AsQueryable();

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
                .Include(p => p.SinhViens)
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
                TempData["SuccessMessage"] = $"Đã cập nhật thông tin phòng {phong.MaPhong}!";
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
    }
}
