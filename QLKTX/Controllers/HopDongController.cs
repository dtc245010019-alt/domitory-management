using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QLKTX.Models;

namespace QLKTX.Controllers
{
    [AdminAuthorize]
    public class HopDongController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HopDongController(ApplicationDbContext context)
        {
            _context = context;
        }

        private SelectList BuildPhongSelectList(string? selectedMaPhong = null)
        {
            var phongs = _context.Phongs
                .Include(p => p.SinhViens)
                .AsNoTracking()
                .ToList()
                .Select(p =>
                {
                    int soDangO = p.SinhViens?.Count(s => s.TinhTrangLuuTru == "Đang ở") ?? 0;
                    int conTrong = Math.Max(0, p.SoLuongGiuong - soDangO);
                    return new
                    {
                        p.MaPhong,
                        ConTrong = conTrong,
                        IsSelected = p.MaPhong == selectedMaPhong,
                        DisplayText = conTrong > 0
                            ? $"P.{p.MaPhong} - {p.LoaiPhong} (Trống {conTrong}/{p.SoLuongGiuong} giường)"
                            : $"P.{p.MaPhong} - {p.LoaiPhong} (ĐÃ ĐẦY {soDangO}/{p.SoLuongGiuong})"
                    };
                })
                .Where(p => p.ConTrong > 0 || p.IsSelected)
                .ToList();

            return new SelectList(phongs, "MaPhong", "DisplayText", selectedMaPhong);
        }

        private async Task DongBoTrangThaiPhongAsync(string? maPhong)
        {
            if (string.IsNullOrEmpty(maPhong)) return;
            var phong = await _context.Phongs.FirstOrDefaultAsync(p => p.MaPhong == maPhong);
            if (phong == null) return;

            int soDangO = await _context.SinhViens.CountAsync(s => s.MaPhong == maPhong && s.TinhTrangLuuTru == "Đang ở");
            if (soDangO >= phong.SoLuongGiuong && phong.TinhTrang == "Còn chỗ")
            {
                phong.TinhTrang = "Đã đầy";
                _context.Phongs.Update(phong);
                await _context.SaveChangesAsync();
            }
            else if (soDangO < phong.SoLuongGiuong && phong.TinhTrang == "Đã đầy")
            {
                phong.TinhTrang = "Còn chỗ";
                _context.Phongs.Update(phong);
                await _context.SaveChangesAsync();
            }
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
            ViewBag.MaSV = new SelectList(_context.SinhViens, "MaSV", "HoTen");
            ViewBag.MaPhong = BuildPhongSelectList();
            return View();
        }

        // POST: HopDong/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(HopDong hopDong)
        {
            ModelState.Remove("SinhVien");
            ModelState.Remove("Phong");
            ModelState.Remove("DieuKhoan");
            hopDong.DieuKhoan ??= "";

            if (ModelState.IsValid)
            {
                if (_context.HopDongs.Any(e => e.MaHopDong == hopDong.MaHopDong))
                {
                    ModelState.AddModelError("MaHopDong", "Mã hợp đồng đã tồn tại!");
                }
                else
                {
                    if (!string.IsNullOrEmpty(hopDong.MaPhong) && hopDong.TrangThai == "Còn hiệu lực")
                    {
                        var phong = await _context.Phongs.FirstOrDefaultAsync(p => p.MaPhong == hopDong.MaPhong);
                        if (phong != null)
                        {
                            int soDangO = await _context.SinhViens.CountAsync(s => s.MaPhong == hopDong.MaPhong && s.TinhTrangLuuTru == "Đang ở" && s.MaSV != hopDong.MaSV);
                            if (soDangO >= phong.SoLuongGiuong)
                            {
                                ModelState.AddModelError("MaPhong", $"Phòng P.{phong.MaPhong} đã đạt tối đa {phong.SoLuongGiuong}/{phong.SoLuongGiuong} giường, không thể lập hợp đồng mới vào phòng này!");
                                ViewBag.MaSV = new SelectList(_context.SinhViens, "MaSV", "HoTen", hopDong.MaSV);
                                ViewBag.MaPhong = BuildPhongSelectList(hopDong.MaPhong);
                                return View(hopDong);
                            }
                        }
                    }

                    _context.Add(hopDong);

                    var sv = await _context.SinhViens.FirstOrDefaultAsync(s => s.MaSV == hopDong.MaSV);
                    string? maPhongCu = sv?.MaPhong;
                    if (sv != null && hopDong.TrangThai == "Còn hiệu lực" && !string.IsNullOrEmpty(hopDong.MaPhong))
                    {
                        sv.MaPhong = hopDong.MaPhong;
                        _context.SinhViens.Update(sv);
                    }

                    await _context.SaveChangesAsync();
                    await DongBoTrangThaiPhongAsync(maPhongCu);
                    await DongBoTrangThaiPhongAsync(hopDong.MaPhong);
                    TempData["SuccessMessage"] = $"Đã lập hợp đồng {hopDong.MaHopDong} thành công!";
                    return RedirectToAction(nameof(Index));
                }
            }
            ViewBag.MaSV = new SelectList(_context.SinhViens, "MaSV", "HoTen", hopDong.MaSV);
            ViewBag.MaPhong = BuildPhongSelectList(hopDong.MaPhong);
            return View(hopDong);
        }

        // GET: HopDong/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return NotFound();

            var hopDong = await _context.HopDongs.FindAsync(id);
            if (hopDong == null) return NotFound();

            ViewBag.MaSV = new SelectList(_context.SinhViens, "MaSV", "HoTen", hopDong.MaSV);
            ViewBag.MaPhong = BuildPhongSelectList(hopDong.MaPhong);
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
            ModelState.Remove("DieuKhoan");
            hopDong.DieuKhoan ??= "";

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(hopDong);
                    await _context.SaveChangesAsync();
                    await DongBoTrangThaiPhongAsync(hopDong.MaPhong);
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.HopDongs.Any(e => e.MaHopDong == hopDong.MaHopDong)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewBag.MaSV = new SelectList(_context.SinhViens, "MaSV", "HoTen", hopDong.MaSV);
            ViewBag.MaPhong = BuildPhongSelectList(hopDong.MaPhong);
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
