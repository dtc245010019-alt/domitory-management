using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLKTX.Models;
using System.Linq;
using System.Threading.Tasks;

namespace QLKTX.Controllers
{
    [AdminAuthorize]
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
            var sinhVienViPham = await _context.ViPhams.Select(v => v.MaSV).Distinct().CountAsync();
            ViewBag.SinhVienViPham = sinhVienViPham;

            // 2. Thống kê Phòng
            var tongPhong = await _context.Phongs.CountAsync();
            var phongConCho = await _context.Phongs.CountAsync(p => p.TinhTrang == "Còn chỗ");
            var phongDaDay = await _context.Phongs.CountAsync(p => p.TinhTrang == "Đã đầy");

            // 3. Thống kê Doanh thu & Công nợ
            var tongDoanhThuCoc = await _context.HopDongs
                .Where(h => h.TrangThai == "Còn hiệu lực")
                .SumAsync(h => (decimal?)h.TienDatCoc) ?? 0m;

            var tongHoaDonDaThu = await _context.HoaDons
                .Where(h => h.TrangThai == "Đã thanh toán")
                .SumAsync(h => (decimal?)h.TongTien) ?? 0m;

            var tongCongNo = await _context.HoaDons
                .Where(h => h.TrangThai == "Chưa thanh toán")
                .SumAsync(h => (decimal?)h.TongTien) ?? 0m;

            var soHoaDonNo = await _context.HoaDons
                .CountAsync(h => h.TrangThai == "Chưa thanh toán");

            // 4. Thống kê Phản ánh / Sự cố
            var suCoChoXuLy = await _context.BaoCaoSuCos
                .CountAsync(b => b.TrangThai == "Chờ xử lý");

            var suCoDangXuLy = await _context.BaoCaoSuCos
                .CountAsync(b => b.TrangThai == "Đang xử lý");

            var suCoDaXuLy = await _context.BaoCaoSuCos
                .CountAsync(b => b.TrangThai == "Đã xử lý" || b.TrangThai == "Đã hoàn thành");

            // Truyền các con số tổng quát sang View qua ViewBag
            ViewBag.TongSinhVien = tongSinhVien;
            ViewBag.TongPhong = tongPhong;
            ViewBag.PhongConCho = phongConCho;
            ViewBag.PhongDaDay = phongDaDay;
            ViewBag.TongDoanhThu = tongDoanhThuCoc;
            ViewBag.TongHoaDonDaThu = tongHoaDonDaThu;
            ViewBag.TongCongNo = tongCongNo;
            ViewBag.SoHoaDonNo = soHoaDonNo;
            ViewBag.SuCoChoXuLy = suCoChoXuLy;
            ViewBag.SuCoDangXuLy = suCoDangXuLy;
            ViewBag.SuCoDaXuLy = suCoDaXuLy;

            // Dữ liệu cho Biểu đồ Tròn: Tỉ lệ phòng
            ViewBag.ChartPhongLabels = new[] { "Còn chỗ", "Đã đầy" };
            ViewBag.ChartPhongData = new[] { phongConCho, phongDaDay };

            // Dữ liệu cho Biểu đồ Cột: Sinh viên theo Loại phòng
            var svTheoLoaiPhong = await _context.SinhViens
                .Include(s => s.Phong)
                .Where(s => s.Phong != null && s.TinhTrangLuuTru == "Đang ở")
                .GroupBy(s => s.Phong!.LoaiPhong)
                .Select(g => new { LoaiPhong = g.Key, SoLuong = g.Count() })
                .ToListAsync();

            ViewBag.ChartSVLabels = svTheoLoaiPhong.Select(x => x.LoaiPhong).ToArray();
            ViewBag.ChartSVData = svTheoLoaiPhong.Select(x => x.SoLuong).ToArray();

            return View();
        }
    }
}
