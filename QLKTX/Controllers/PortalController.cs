using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QLKTX.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<PortalController> _logger;

        public PortalController(
            ApplicationDbContext context, 
            IWebHostEnvironment webHostEnvironment,
            ILogger<PortalController> logger)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
        }

        /// <summary>
        /// Helper kiểm tra Session sinh viên đăng nhập.
        /// Trả về null nếu session hợp lệ, hoặc RedirectResult về trang Đăng nhập nếu session rỗng kèm theo log cảnh báo.
        /// </summary>
        private IActionResult? EnsureStudentSession(string actionName, out string maSV)
        {
            maSV = HttpContext.Session.GetString("MaSV_DangNhap") ?? string.Empty;
            if (string.IsNullOrEmpty(maSV))
            {
                _logger.LogWarning("[{Timestamp}] Session rỗng khi truy cập {Action}, SessionId={Id}", 
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), actionName, HttpContext.Session.Id);
                return RedirectToAction("Dangnhap", "DN");
            }
            return null;
        }

        // ==========================================
        // 1. TRANG CHỦ SINH VIÊN
        // ==========================================
        public async Task<IActionResult> Index()
        {
            if (EnsureStudentSession(nameof(Index), out string maSV) is { } redirect)
                return redirect;

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
            if (EnsureStudentSession(nameof(HoaDon), out string maSV) is { } redirect)
                return redirect;

            var hoaDons = await _context.HoaDons
                .Where(h => h.MaSV == maSV)
                .OrderByDescending(h => h.NgayLap)
                .ToListAsync();

            return View(hoaDons);
        }

        [HttpPost]
        public async Task<IActionResult> ThanhToan(string maHoaDon, string phuongThuc, string matKhauXacNhan)
        {
            if (EnsureStudentSession(nameof(ThanhToan), out string maSV) is { } redirect)
                return redirect;

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
            if (EnsureStudentSession(nameof(BaoCaoSuCo), out string maSV) is { } redirect)
                return redirect;

            var suCos = await _context.BaoCaoSuCos
                .Where(b => b.MaSV == maSV)
                .OrderByDescending(b => b.NgayBaoCao)
                .ToListAsync();

            return View(suCos);
        }

        [HttpPost]
        public async Task<IActionResult> GuiBaoCao(string TenSuCo, string MoTa, string MucDoKhanCap, IFormFile HinhAnhFile)
        {
            if (EnsureStudentSession(nameof(GuiBaoCao), out string maSV) is { } redirect)
                return redirect;

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
            if (EnsureStudentSession(nameof(TaoDuLieuMau), out string maSV) is { } redirect)
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

        // ==========================================
        // 5. HELPER: TRANG QUẢN TRỊ ADMIN
        // ==========================================
        private IActionResult? EnsureAdminSession(string actionName)
        {
            var isAdmin = HttpContext.Session.GetString("Admin_DangNhap");
            if (isAdmin != "true")
            {
                _logger.LogWarning("[{Timestamp}] Truy cập trái phép {Action} (Yêu cầu Admin).", 
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), actionName);
                return RedirectToAction("Dangnhap", "DN");
            }
            return null;
        }

        public async Task<IActionResult> QuanLyHoaDonAdmin(string searchString, string trangThai, string loaiHoaDon)
        {
            if (EnsureAdminSession(nameof(QuanLyHoaDonAdmin)) is { } redirect)
                return redirect;

            ViewData["CurrentSearch"] = searchString;
            ViewData["CurrentTrangThai"] = trangThai;
            ViewData["CurrentLoai"] = loaiHoaDon;

            var query = _context.HoaDons
                .Include(h => h.SinhVien)
                .Include(h => h.Phong)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                // Normalize for search: searchString will be evaluated directly via EF.
                string searchLower = searchString.ToLower();
                query = query.Where(h => 
                    h.MaHoaDon.ToLower().Contains(searchLower) ||
                    h.MaSV.ToLower().Contains(searchLower) ||
                    h.SinhVien.HoTen.ToLower().Contains(searchLower) ||
                    h.MaPhong.ToLower().Contains(searchLower) ||
                    h.Phong.MaPhong.ToLower().Contains(searchLower)
                );
            }

            if (!string.IsNullOrEmpty(trangThai) && trangThai != "Tất cả")
            {
                query = query.Where(h => h.TrangThai == trangThai);
            }

            if (!string.IsNullOrEmpty(loaiHoaDon) && loaiHoaDon != "Tất cả")
            {
                query = query.Where(h => h.LoaiHoaDon == loaiHoaDon);
            }

            var hoaDons = await query
                .OrderByDescending(h => h.NgayLap)
                .ToListAsync();

            return View(hoaDons);
        }

        [HttpPost]
        public async Task<IActionResult> CapNhatTrangThaiHoaDon(string maHoaDon, string trangThaiMoi)
        {
            if (EnsureAdminSession(nameof(CapNhatTrangThaiHoaDon)) is { } redirect)
                return redirect;

            var hd = await _context.HoaDons.FirstOrDefaultAsync(h => h.MaHoaDon == maHoaDon);
            if (hd != null)
            {
                hd.TrangThai = trangThaiMoi;
                if (trangThaiMoi == "Đã thanh toán" && string.IsNullOrEmpty(hd.HinhThucThanhToan))
                {
                    hd.HinhThucThanhToan = "Tiền mặt (Admin thu)";
                }
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Cập nhật trạng thái hóa đơn {maHoaDon} thành công!";
            }
            else
            {
                TempData["ErrorMessage"] = $"Không tìm thấy hóa đơn {maHoaDon}!";
            }

            return RedirectToAction(nameof(QuanLyHoaDonAdmin));
        }

        [HttpGet]
        public async Task<IActionResult> TaoHoaDon()
        {
            if (EnsureAdminSession(nameof(TaoHoaDon)) is { } redirect)
                return redirect;

            // Lấy danh sách sinh viên đang ở
            var sinhViens = await _context.SinhViens
                .Where(s => s.TinhTrangLuuTru == "Đang ở")
                .Select(s => new {
                    MaSV = s.MaSV,
                    Display = $"{s.MaSV} - {s.HoTen} (Phòng: {s.MaPhong})"
                })
                .ToListAsync();

            ViewBag.SinhVienList = new SelectList(sinhViens, "MaSV", "Display");

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> TaoHoaDon(string maSV, string loaiHoaDon, int? chiSoCu, int? chiSoMoi, decimal? donGiaTuNhap)
        {
            if (EnsureAdminSession(nameof(TaoHoaDon)) is { } redirect)
                return redirect;

            if (string.IsNullOrEmpty(maSV))
            {
                ModelState.AddModelError("maSV", "Vui lòng chọn Sinh viên.");
            }

            if (loaiHoaDon == "Điện nước")
            {
                if (!chiSoCu.HasValue || !chiSoMoi.HasValue)
                {
                    ModelState.AddModelError("", "Vui lòng nhập đầy đủ Chỉ số cũ và Chỉ số mới.");
                }
                else if (chiSoMoi.Value <= chiSoCu.Value)
                {
                    ModelState.AddModelError("chiSoMoi", "Chỉ số mới phải lớn hơn Chỉ số cũ.");
                }
            }

            if (!ModelState.IsValid)
            {
                var sinhViens = await _context.SinhViens
                    .Where(s => s.TinhTrangLuuTru == "Đang ở")
                    .Select(s => new {
                        MaSV = s.MaSV,
                        Display = $"{s.MaSV} - {s.HoTen} (Phòng: {s.MaPhong})"
                    })
                    .ToListAsync();
                ViewBag.SinhVienList = new SelectList(sinhViens, "MaSV", "Display", maSV);
                return View();
            }

            // Lấy thông tin sinh viên
            var sv = await _context.SinhViens
                .Include(s => s.Phong)
                .FirstOrDefaultAsync(s => s.MaSV == maSV);

            if (sv == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin sinh viên.";
                return RedirectToAction(nameof(QuanLyHoaDonAdmin));
            }

            string maHoaDon = $"HD_{DateTime.Now.Ticks.ToString().Substring(8)}";
            decimal tongTien = 0;
            decimal donGia = donGiaTuNhap ?? 0;

            if (loaiHoaDon == "Tiền phòng")
            {
                tongTien = sv.Phong?.GiaPhong ?? 0;
                donGia = tongTien;
            }
            else if (loaiHoaDon == "Điện nước")
            {
                donGia = donGiaTuNhap ?? 3500; // Đơn giá mặc định nếu không nhập
                tongTien = (chiSoMoi!.Value - chiSoCu!.Value) * donGia;
            }

            var newHoaDon = new HoaDon
            {
                MaHoaDon = maHoaDon,
                LoaiHoaDon = loaiHoaDon,
                ChiSoCu = loaiHoaDon == "Điện nước" ? chiSoCu : null,
                ChiSoMoi = loaiHoaDon == "Điện nước" ? chiSoMoi : null,
                DonGia = donGia,
                TongTien = tongTien,
                HinhThucThanhToan = "Chưa có",
                TrangThai = "Chưa thanh toán",
                NgayLap = DateTime.Now,
                MaSV = maSV,
                MaPhong = sv.MaPhong
            };

            _context.HoaDons.Add(newHoaDon);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Thêm mới hóa đơn {maHoaDon} thành công!";
            return RedirectToAction(nameof(QuanLyHoaDonAdmin));
        }

        public async Task<IActionResult> QuanLySuCoAdmin()
        {
            if (EnsureAdminSession(nameof(QuanLySuCoAdmin)) is { } redirect)
                return redirect;

            var suCos = await _context.BaoCaoSuCos
                .Include(b => b.SinhVien)
                .Include(b => b.Phong)
                .OrderByDescending(b => b.NgayBaoCao)
                .ToListAsync();

            return View(suCos);
        }

        [HttpPost]
        public async Task<IActionResult> CapNhatTrangThaiSuCo(string maSuCo, string trangThaiMoi)
        {
            if (EnsureAdminSession(nameof(CapNhatTrangThaiSuCo)) is { } redirect)
                return redirect;

            var sc = await _context.BaoCaoSuCos.FirstOrDefaultAsync(b => b.MaSuCo == maSuCo);
            if (sc != null)
            {
                sc.TrangThai = trangThaiMoi;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Cập nhật trạng thái sự cố {maSuCo} thành công!";
            }
            else
            {
                TempData["ErrorMessage"] = $"Không tìm thấy sự cố {maSuCo}!";
            }

            return RedirectToAction(nameof(QuanLySuCoAdmin));
        }
    }
}