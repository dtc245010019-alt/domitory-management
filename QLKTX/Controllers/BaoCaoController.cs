using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLKTX.Models;

namespace QLKTX.Controllers
{
    public class BaoCaoController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BaoCaoController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // 1. Thống kê Sinh viên
            var tongSinhVien = await _context.SinhViens.CountAsync(s => s.TinhTrangLuuTru == "Đang ở");
            //Thống kê sinh viên bị kỉ luật
            var sinhVienViPham = await _context.ViPhams.Select(v => v.MaSV).Distinct().CountAsync();
            ViewBag.SinhVienViPham = sinhVienViPham;

            // 2. Thống kê Phòng
            var tongPhong = await _context.Phongs.CountAsync();
            var phongConCho = await _context.Phongs.CountAsync(p => p.TinhTrang == "Còn chỗ");
            var phongDaDay = await _context.Phongs.CountAsync(p => p.TinhTrang == "Đã đầy");

            // 3. Doanh thu (Tạm tính tổng tiền cọc từ Hợp đồng đang hiệu lực)
            var tongDoanhThu = await _context.HopDongs
                .Where(h => h.TrangThai == "Còn hiệu lực")
                .SumAsync(h => h.TienDatCoc);

            // Truyền các con số tổng quát sang View qua ViewBag
            ViewBag.TongSinhVien = tongSinhVien;
            ViewBag.TongPhong = tongPhong;
            ViewBag.PhongConCho = phongConCho;
            ViewBag.PhongDaDay = phongDaDay;
            ViewBag.TongDoanhThu = tongDoanhThu;

          
            // Dữ liệu cho Biểu đồ Tròn: Tỉ lệ phòng
            
            ViewBag.ChartPhongLabels = new[] { "Còn chỗ", "Đã đầy" };
            ViewBag.ChartPhongData = new[] { phongConCho, phongDaDay };

            
            // Dữ liệu cho Biểu đồ Cột: Sinh viên theo Loại phòng
            
            var svTheoLoaiPhong = await _context.SinhViens
                .Include(s => s.Phong)
                .Where(s => s.Phong != null && s.TinhTrangLuuTru == "Đang ở")
                .GroupBy(s => s.Phong.LoaiPhong)
                .Select(g => new { LoaiPhong = g.Key, SoLuong = g.Count() })
                .ToListAsync();

            ViewBag.ChartSVLabels = svTheoLoaiPhong.Select(x => x.LoaiPhong).ToArray();
            ViewBag.ChartSVData = svTheoLoaiPhong.Select(x => x.SoLuong).ToArray();

            return View();
        }
    }
}