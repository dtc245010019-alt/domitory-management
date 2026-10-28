using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QLKTX.Models;

namespace QLKTX.Controllers
{
    [AdminAuthorize(OnlyAdmin = true)]
    public class ViPhamController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ViPhamController(ApplicationDbContext context)
        {
            _context = context;
        }

        private async Task DongBoTrangThaiPhongAsync(string? maPhong)
        {
            if (string.IsNullOrEmpty(maPhong)) return;
            var phong = await _context.Phongs.FirstOrDefaultAsync(p => p.MaPhong == maPhong);
            if (phong != null && phong.TinhTrang != "Bảo trì" && phong.TinhTrang != "Đang sửa chữa")
            {
                int soDangO = await _context.SinhViens.CountAsync(s => s.MaPhong == maPhong && s.TinhTrangLuuTru == "Đang ở");
                phong.TinhTrang = (soDangO >= phong.SoLuongGiuong) ? "Đã đầy" : "Còn chỗ";
                _context.Phongs.Update(phong);
                await _context.SaveChangesAsync();
            }
        }

        // GET: ViPham
        public async Task<IActionResult> Index()
        {
            // Lấy danh sách vi phạm kèm theo thông tin Sinh viên
            var danhSachViPham = await _context.ViPhams
                .Include(v => v.SinhVien)
                .OrderByDescending(v => v.NgayViPham)
                .ToListAsync();
            return View(danhSachViPham);
        }

        // GET: ViPham/Create
        public IActionResult Create()
        {
            // Hiển thị dropdown chọn Sinh viên (Hiển thị Mã SV + Họ Tên cho dễ chọn)
            var sinhViens = _context.SinhViens.Select(s => new {
                MaSV = s.MaSV,
                ThongTin = s.MaSV + " - " + s.HoTen
            }).ToList();

            ViewBag.MaSV = new SelectList(sinhViens, "MaSV", "ThongTin");
            return View();
        }

        // POST: ViPham/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ViPham viPham)
        {
            ModelState.Remove("SinhVien");
            ModelState.Remove("GhiChu");
            viPham.GhiChu ??= "";

            if (ModelState.IsValid)
            {
                _context.Add(viPham);
                await _context.SaveChangesAsync();

                // Nếu hình thức là "Đình chỉ", tự động cập nhật tình trạng sinh viên
                if (viPham.HinhThucXuLy != null && viPham.HinhThucXuLy.Contains("Đình chỉ"))
                {
                    var sv = await _context.SinhViens.FindAsync(viPham.MaSV);
                    if (sv != null)
                    {
                        sv.TinhTrangLuuTru = "Đình chỉ";
                        _context.Update(sv);
                        await _context.SaveChangesAsync();

                        if (!string.IsNullOrEmpty(sv.MaPhong))
                        {
                            var phong = await _context.Phongs.FirstOrDefaultAsync(p => p.MaPhong == sv.MaPhong);
                            if (phong != null && phong.TinhTrang == "Đã đầy")
                            {
                                int soDangO = await _context.SinhViens.CountAsync(s => s.MaPhong == sv.MaPhong && s.TinhTrangLuuTru == "Đang ở");
                                if (soDangO < phong.SoLuongGiuong)
                                {
                                    phong.TinhTrang = "Còn chỗ";
                                    _context.Update(phong);
                                    await _context.SaveChangesAsync();
                                }
                            }
                        }
                    }
                }

                TempData["SuccessMessage"] = "Đã lập biên bản vi phạm thành công!";
                return RedirectToAction(nameof(Index));
            }

            var sinhViens = _context.SinhViens.Select(s => new {
                MaSV = s.MaSV,
                ThongTin = s.MaSV + " - " + s.HoTen
            }).ToList();
            ViewBag.MaSV = new SelectList(sinhViens, "MaSV", "ThongTin", viPham.MaSV);
            return View(viPham);
        }

        // GET: ViPham/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var viPham = await _context.ViPhams.FindAsync(id);
            if (viPham == null) return NotFound();

            var sinhViens = _context.SinhViens.Select(s => new {
                MaSV = s.MaSV,
                ThongTin = s.MaSV + " - " + s.HoTen
            }).ToList();
            ViewBag.MaSV = new SelectList(sinhViens, "MaSV", "ThongTin", viPham.MaSV);

            return View(viPham);
        }

        // POST: ViPham/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ViPham viPham)
        {
            if (id != viPham.MaViPham) return NotFound();

            ModelState.Remove("SinhVien");
            ModelState.Remove("GhiChu");
            viPham.GhiChu ??= "";

            if (ModelState.IsValid)
            {
                try
                {
                    var oldViPham = await _context.ViPhams.AsNoTracking().FirstOrDefaultAsync(v => v.MaViPham == id);
                    string oldHinhThuc = oldViPham?.HinhThucXuLy ?? "";
                    string newHinhThuc = viPham.HinhThucXuLy ?? "";

                    _context.Update(viPham);
                    await _context.SaveChangesAsync();

                    // Hoàn trả hoặc cập nhật lại trạng thái sinh viên khi sửa mức kỷ luật
                    var sv = await _context.SinhViens.FindAsync(viPham.MaSV);
                    if (sv != null)
                    {
                        if (oldHinhThuc.Contains("Đình chỉ") && !newHinhThuc.Contains("Đình chỉ"))
                        {
                            bool hasOtherSuspension = await _context.ViPhams.AnyAsync(v => v.MaSV == sv.MaSV && v.MaViPham != id && v.HinhThucXuLy != null && v.HinhThucXuLy.Contains("Đình chỉ"));
                            if (!hasOtherSuspension && sv.TinhTrangLuuTru == "Đình chỉ")
                            {
                                sv.TinhTrangLuuTru = "Đang ở";
                                _context.Update(sv);
                                await _context.SaveChangesAsync();
                                await DongBoTrangThaiPhongAsync(sv.MaPhong);
                            }
                        }
                        else if (!oldHinhThuc.Contains("Đình chỉ") && newHinhThuc.Contains("Đình chỉ"))
                        {
                            sv.TinhTrangLuuTru = "Đình chỉ";
                            _context.Update(sv);
                            await _context.SaveChangesAsync();
                            await DongBoTrangThaiPhongAsync(sv.MaPhong);
                        }
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.ViPhams.Any(e => e.MaViPham == viPham.MaViPham)) return NotFound();
                    else throw;
                }
                TempData["SuccessMessage"] = "Cập nhật biên bản vi phạm thành công!";
                return RedirectToAction(nameof(Index));
            }

            var sinhViens = _context.SinhViens.Select(s => new {
                MaSV = s.MaSV,
                ThongTin = s.MaSV + " - " + s.HoTen
            }).ToList();
            ViewBag.MaSV = new SelectList(sinhViens, "MaSV", "ThongTin", viPham.MaSV);
            return View(viPham);
        }

        // GET: ViPham/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var viPham = await _context.ViPhams
                .Include(v => v.SinhVien)
                .FirstOrDefaultAsync(m => m.MaViPham == id);

            if (viPham == null) return NotFound();

            return View(viPham);
        }

        // POST: ViPham/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var viPham = await _context.ViPhams.FindAsync(id);
            if (viPham != null)
            {
                string hinhThuc = viPham.HinhThucXuLy ?? "";
                string maSV = viPham.MaSV;

                _context.ViPhams.Remove(viPham);
                await _context.SaveChangesAsync();

                // Nếu xóa biên bản đình chỉ và sinh viên không còn vi phạm đình chỉ nào khác -> phục hồi Đang ở
                if (hinhThuc.Contains("Đình chỉ"))
                {
                    bool hasOtherSuspension = await _context.ViPhams.AnyAsync(v => v.MaSV == maSV && v.HinhThucXuLy != null && v.HinhThucXuLy.Contains("Đình chỉ"));
                    if (!hasOtherSuspension)
                    {
                        var sv = await _context.SinhViens.FindAsync(maSV);
                        if (sv != null && sv.TinhTrangLuuTru == "Đình chỉ")
                        {
                            sv.TinhTrangLuuTru = "Đang ở";
                            _context.Update(sv);
                            await _context.SaveChangesAsync();
                            await DongBoTrangThaiPhongAsync(sv.MaPhong);
                        }
                    }
                }

                TempData["SuccessMessage"] = "Đã xóa bản ghi vi phạm!";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
