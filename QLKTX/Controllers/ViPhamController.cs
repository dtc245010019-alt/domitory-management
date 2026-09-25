using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QLKTX.Models;

namespace QLKTX.Controllers
{
    public class ViPhamController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ViPhamController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: ViPham
        public async Task<IActionResult> Index()
        {
            // Lấy danh sách vi phạm kèm theo thông tin Sinh viên
            var danhSachViPham = await _context.ViPhams.Include(v => v.SinhVien).ToListAsync();
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
            if (ModelState.IsValid)
            {
                _context.Add(viPham);
                await _context.SaveChangesAsync();

                // Nếu hình thức là "Đình chỉ", tự động cập nhật tình trạng sinh viên
                if (viPham.HinhThucXuLy == "Đình chỉ")
                {
                    var sv = await _context.SinhViens.FindAsync(viPham.MaSV);
                    if (sv != null)
                    {
                        sv.TinhTrangLuuTru = "Đình chỉ";
                        _context.Update(sv);
                        await _context.SaveChangesAsync();
                    }
                }

                return RedirectToAction(nameof(Index));
            }
            ViewBag.MaSV = new SelectList(_context.SinhViens, "MaSV", "HoTen", viPham.MaSV);
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

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(viPham);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.ViPhams.Any(e => e.MaViPham == viPham.MaViPham)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewBag.MaSV = new SelectList(_context.SinhViens, "MaSV", "HoTen", viPham.MaSV);
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
                _context.ViPhams.Remove(viPham);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}