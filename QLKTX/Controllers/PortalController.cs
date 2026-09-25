using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLKTX.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QLKTX.Controllers
{
    public class PortalController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public PortalController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // Lấy Mã Sinh Viên từ Session đăng nhập
        private string GetCurrentMaSV()
        {
            return HttpContext.Session.GetString("MaSV_DangNhap");
        }

        // ==========================================
        // 1. TRANG CHỦ SINH VIÊN
        // ==========================================
        public async Task<IActionResult> Index()
        {
            string maSV = GetCurrentMaSV();
            if (string.IsNullOrEmpty(maSV)) 
                return RedirectToAction("Dangnhap", "DN");

            var sv = await _context.SinhViens
                .Include(s => s.Phong)
                .FirstOrDefaultAsync(m => m.MaSV == maSV);

            if (sv == null) 
                return NotFound("Không tìm thấy thông tin sinh viên.");

            return View(sv);
        }

        // ==========================================
        // 2. TRANG HÓA ĐƠN & THANH TOÁN
        // ==========================================
        public async Task<IActionResult> HoaDon()
        {
            string maSV = GetCurrentMaSV();
            if (string.IsNullOrEmpty(maSV)) 
                return RedirectToAction("Dangnhap", "DN");

            var hoaDons = await _context.HoaDons
                .Where(h => h.MaSV == maSV)
                .OrderByDescending(h => h.NgayLap)
                .ToListAsync();

            return View(hoaDons);
        }

        [HttpPost]
        public async Task<IActionResult> ThanhToan(string maHoaDon, string phuongThuc, string matKhauXacNhan)
        {
            string maSV = GetCurrentMaSV();
            if (string.IsNullOrEmpty(maSV)) 
                return RedirectToAction("Dangnhap", "DN");

            // Xác minh mật khẩu đăng nhập của sinh viên
            var account = await _context.TaiKhoans
                .FirstOrDefaultAsync(a => a.MaSV == maSV && a.MatKhau == matKhauXacNhan);

            if (account == null)
            {
                TempData["ErrorMessage"] = "Xác minh thất bại: Mật khẩu không chính xác. Giao dịch đã bị hủy!";
                return RedirectToAction("HoaDon");
            }

            // Xử lý cập nhật trạng thái hóa đơn
            var hd = await _context.HoaDons.FirstOrDefaultAsync(h => h.MaHoaDon == maHoaDon && h.MaSV == maSV);
            if (hd != null && hd.TrangThai != "Đã thanh toán")
            {
                hd.TrangThai = "Đã thanh toán";
                hd.HinhThucThanhToan = phuongThuc;

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Thanh toán thành công hóa đơn {maHoaDon} qua {phuongThuc}!";
            }

            return RedirectToAction("HoaDon");
        }

        // ==========================================
        // 3. TRANG BÁO CÁO SỰ CỐ (SINH VIÊN)
        // ==========================================
        public async Task<IActionResult> BaoCaoSuCo()
        {
            string maSV = GetCurrentMaSV();
            if (string.IsNullOrEmpty(maSV)) 
                return RedirectToAction("Dangnhap", "DN");

            var suCos = await _context.BaoCaoSuCos
                .Where(b => b.MaSV == maSV)
                .OrderByDescending(b => b.NgayBaoCao)
                .ToListAsync();

            return View(suCos);
        }

        [HttpPost]
        public async Task<IActionResult> GuiBaoCao(string TenSuCo, string MoTa, string MucDoKhanCap, IFormFile HinhAnhFile)
        {
            string maSV = GetCurrentMaSV();
            if (string.IsNullOrEmpty(maSV)) 
                return RedirectToAction("Dangnhap", "DN");

            // Tìm thông tin Sinh viên để lấy Mã Phòng
            var sv = await _context.SinhViens.FirstOrDefaultAsync(s => s.MaSV == maSV);
            if (sv == null || string.IsNullOrEmpty(sv.MaPhong))
            {
                TempData["ErrorMessage"] = "Lỗi: Bạn chưa được xếp phòng nên không thể báo cáo sự cố phòng.";
                return RedirectToAction("BaoCaoSuCo");
            }

            string maSC = "SC_" + DateTime.Now.ToString("yyyyMMddHHmm");
            string duongDanAnh = "";

            // Lưu ảnh đính kèm vào thư mục wwwroot/uploads/suco
            if (HinhAnhFile != null && HinhAnhFile.Length > 0)
            {
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "suco");
                if (!Directory.Exists(uploadsFolder)) 
                    Directory.CreateDirectory(uploadsFolder);

                string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(HinhAnhFile.FileName);
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await HinhAnhFile.CopyToAsync(fileStream);
                }
                duongDanAnh = "/uploads/suco/" + uniqueFileName;
            }

            // Tạo đối tượng báo cáo sự cố mới
            var bc = new BaoCaoSuCo
            {
                MaSuCo = maSC,
                TenSuCo = TenSuCo,
                MoTa = MoTa,
                MucDoKhanCap = MucDoKhanCap,
                HinhAnh = duongDanAnh,
                MaSV = maSV,
                MaPhong = sv.MaPhong,
                TrangThai = "Chờ xử lý", // Trạng thái mặc định ban đầu
                NgayBaoCao = DateTime.Now
            };

            _context.BaoCaoSuCos.Add(bc);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Gửi báo cáo sự cố thành công! Ban quản lý sẽ sớm kiểm tra và xử lý.";
            return RedirectToAction("BaoCaoSuCo");
        }

        // ==========================================
        // 4. HÀM TẠO DỮ LIỆU MẪU (TEST HÓA ĐƠN)
        // ==========================================
        public async Task<IActionResult> TaoDuLieuMau()
        {
            string maSV = GetCurrentMaSV();
            if (string.IsNullOrEmpty(maSV)) 
                return Content("Lỗi: Vui lòng đăng nhập bằng tài khoản Sinh viên trước!");

            var sv = await _context.SinhViens.FirstOrDefaultAsync(s => s.MaSV == maSV);
            if (sv == null) 
                return Content("Lỗi: Không tìm thấy Sinh viên trong DB!");

            string maPhong = sv.MaPhong ?? "101";

            var fakeHoaDons = new List<HoaDon>
            {
                new HoaDon {
                    MaHoaDon = "HD_" + DateTime.Now.ToString("HHmmss") + "1",
                    LoaiHoaDon = "Tiền phòng",
                    DonGia = 1500000,
                    TongTien = 1500000,
                    TrangThai = "Chưa thanh toán",
                    HinhThucThanhToan = "Chưa có",
                    NgayLap = DateTime.Now,
                    MaSV = maSV,
                    MaPhong = maPhong
                },
                new HoaDon {
                    MaHoaDon = "HD_" + DateTime.Now.ToString("HHmmss") + "2",
                    LoaiHoaDon = "Điện nước",
                    ChiSoCu = 100,
                    ChiSoMoi = 155,
                    DonGia = 3500,
                    TongTien = 192500,
                    TrangThai = "Chưa thanh toán",
                    HinhThucThanhToan = "Chưa có",
                    NgayLap = DateTime.Now.AddDays(-2),
                    MaSV = maSV,
                    MaPhong = maPhong
                },
                new HoaDon {
                    MaHoaDon = "HD_" + DateTime.Now.ToString("HHmmss") + "3",
                    LoaiHoaDon = "Tiền phòng",
                    DonGia = 1500000,
                    TongTien = 1500000,
                    HinhThucThanhToan = "Ví MoMo",
                    TrangThai = "Đã thanh toán",
                    NgayLap = DateTime.Now.AddMonths(-1),
                    MaSV = maSV,
                    MaPhong = maPhong
                }
            };

            _context.HoaDons.AddRange(fakeHoaDons);
            await _context.SaveChangesAsync();

            return Content("🎉 Đã tạo 3 Hóa đơn mẫu thành công! Hãy bấm Back trên trình duyệt và F5 lại trang Hóa Đơn.");
        }
    }
}