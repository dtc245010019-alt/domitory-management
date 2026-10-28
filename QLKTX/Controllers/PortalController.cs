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
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public PortalController(
            ApplicationDbContext context, 
            IWebHostEnvironment webHostEnvironment,
            ILogger<PortalController> logger,
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
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

        [HttpPost]
        public async Task<IActionResult> GuiYeuCauPhong(string LoaiYeuCau, string PhongMongMuon, string LyDo)
        {
            if (EnsureStudentSession(nameof(GuiYeuCauPhong), out string maSV) is { } redirect)
                return redirect;

            var sv = await _context.SinhViens.FirstOrDefaultAsync(s => s.MaSV == maSV);
            if (sv == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy hồ sơ sinh viên.";
                return RedirectToAction(nameof(Index));
            }

            string maPhongHienTai = !string.IsNullOrEmpty(sv.MaPhong) ? sv.MaPhong : (_context.Phongs.FirstOrDefault()?.MaPhong ?? "101");
            var yeuCau = new BaoCaoSuCo
            {
                MaSuCo = "YC_" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                TenSuCo = $"[{LoaiYeuCau}] Nguyện vọng: {PhongMongMuon}",
                MoTa = LyDo ?? "Sinh viên gửi yêu cầu qua Portal",
                MucDoKhanCap = "Bình thường",
                HinhAnh = "",
                MaSV = maSV,
                MaPhong = maPhongHienTai,
                TrangThai = "Chờ xử lý",
                NgayBaoCao = DateTime.Now
            };

            _context.BaoCaoSuCos.Add(yeuCau);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã gửi yêu cầu '{LoaiYeuCau}' ({PhongMongMuon}) tới Ban Quản lý thành công!";
            return RedirectToAction(nameof(Index));
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
        public async Task<IActionResult> GuiBaoCao(string TenSuCo, string MoTa, string MucDoKhanCap, IFormFile? HinhAnhFile)
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

            string maSC = "SC_" + DateTime.Now.ToString("yyyyMMddHHmmss");
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

        private IActionResult RedirectToLocal(string? returnUrl, string defaultAction = nameof(BaoCaoSuCo))
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(defaultAction);
        }

        [HttpPost]
        public async Task<IActionResult> XoaYeuCauSinhVien(string maSuCo, string? returnUrl = null)
        {
            if (EnsureStudentSession(nameof(XoaYeuCauSinhVien), out string maSV) is { } redirect)
                return redirect;

            if (string.IsNullOrWhiteSpace(maSuCo))
            {
                TempData["ErrorMessage"] = "Mã yêu cầu không hợp lệ!";
                return RedirectToLocal(returnUrl);
            }

            var item = await _context.BaoCaoSuCos.FirstOrDefaultAsync(b => b.MaSuCo == maSuCo && b.MaSV == maSV);
            if (item == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy yêu cầu hoặc bạn không có quyền xóa yêu cầu này!";
                return RedirectToLocal(returnUrl);
            }

            // Chỉ cho phép xóa các yêu cầu đã hoàn thành (Đã xử lý, Đã hoàn thành, Đã duyệt) hoặc Từ chối
            bool isCompleted = string.Equals(item.TrangThai, "Đã xử lý", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(item.TrangThai, "Đã hoàn thành", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(item.TrangThai, "Đã duyệt", StringComparison.OrdinalIgnoreCase);

            bool isRejected = string.Equals(item.TrangThai, "Từ chối", StringComparison.OrdinalIgnoreCase);

            if (!isCompleted && !isRejected)
            {
                TempData["ErrorMessage"] = "Chỉ có thể xóa các yêu cầu đã hoàn thành hoặc bị từ chối!";
                return RedirectToLocal(returnUrl);
            }

            // Xóa file ảnh đính kèm vật lý nếu có
            if (!string.IsNullOrEmpty(item.HinhAnh))
            {
                try
                {
                    string relativePath = item.HinhAnh.TrimStart('/', '\\');
                    string fullPath = Path.Combine(_webHostEnvironment.WebRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(fullPath))
                    {
                        System.IO.File.Delete(fullPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không thể xóa file ảnh sự cố: {Path}", item.HinhAnh);
                }
            }

            _context.BaoCaoSuCos.Remove(item);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã xóa yêu cầu '{item.TenSuCo}' thành công!";
            return RedirectToLocal(returnUrl);
        }

        [HttpPost]
        public async Task<IActionResult> XoaTatCaYeuCauDaHoanThanhVaTuChoi(string? returnUrl = null)
        {
            if (EnsureStudentSession(nameof(XoaTatCaYeuCauDaHoanThanhVaTuChoi), out string maSV) is { } redirect)
                return redirect;

            var items = await _context.BaoCaoSuCos
                .Where(b => b.MaSV == maSV && (b.TrangThai == "Đã xử lý" || b.TrangThai == "Đã hoàn thành" || b.TrangThai == "Đã duyệt" || b.TrangThai == "Từ chối"))
                .ToListAsync();

            if (items.Any())
            {
                int count = items.Count;
                foreach (var it in items)
                {
                    if (!string.IsNullOrEmpty(it.HinhAnh))
                    {
                        try
                        {
                            string relativePath = it.HinhAnh.TrimStart('/', '\\');
                            string fullPath = Path.Combine(_webHostEnvironment.WebRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
                            if (System.IO.File.Exists(fullPath))
                            {
                                System.IO.File.Delete(fullPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Lỗi khi xóa file ảnh sự cố {HinhAnh}", it.HinhAnh);
                        }
                    }
                }

                _context.BaoCaoSuCos.RemoveRange(items);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã xóa thành công {count} yêu cầu đã xong hoặc bị từ chối khỏi lịch sử!";
            }
            else
            {
                TempData["SuccessMessage"] = "Không có yêu cầu nào đã hoàn thành hoặc từ chối để xóa.";
            }

            return RedirectToLocal(returnUrl);
        }

        [HttpPost]
        public async Task<IActionResult> GuiLaiYeuCau(string maSuCo, string? moTaMoi, string? returnUrl = null)
        {
            if (EnsureStudentSession(nameof(GuiLaiYeuCau), out string maSV) is { } redirect)
                return redirect;

            if (string.IsNullOrWhiteSpace(maSuCo))
            {
                TempData["ErrorMessage"] = "Mã yêu cầu không hợp lệ!";
                return RedirectToLocal(returnUrl);
            }

            var item = await _context.BaoCaoSuCos.FirstOrDefaultAsync(b => b.MaSuCo == maSuCo && b.MaSV == maSV);
            if (item == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy yêu cầu hoặc bạn không có quyền thao tác!";
                return RedirectToLocal(returnUrl);
            }

            if (!string.Equals(item.TrangThai, "Từ chối", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Chỉ có thể gửi lại các yêu cầu có trạng thái 'Từ chối'!";
                return RedirectToLocal(returnUrl);
            }

            // Đổi trạng thái về Chờ xử lý và làm mới thời gian gửi báo cáo
            item.TrangThai = "Chờ xử lý";
            item.NgayBaoCao = DateTime.Now;

            if (!string.IsNullOrWhiteSpace(moTaMoi))
            {
                item.MoTa = moTaMoi.Replace(" [Đã đồng bộ]", "").Trim();
            }
            else
            {
                item.MoTa = (item.MoTa ?? "").Replace(" [Đã đồng bộ]", "").Trim();
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã gửi lại yêu cầu '{item.TenSuCo}' thành công! Ban Quản lý sẽ sớm xét duyệt lại.";
            return RedirectToLocal(returnUrl);
        }

        // ==========================================
        // 4. HÀM TẠO DỮ LIỆU MẪU (TEST HÓA ĐƠN) - CHỈ DÀNH CHO ADMIN
        // ==========================================
        [AdminAuthorize(OnlyAdmin = true)]
        public async Task<IActionResult> TaoDuLieuMau()
        {
            if (EnsureAdminSession(nameof(TaoDuLieuMau)) is { } redirectAdmin)
                return redirectAdmin;

            string maSV = HttpContext.Session.GetString("MaSV_DangNhap") ?? "DTC001";

            var sv = await _context.SinhViens.Include(s => s.Phong).FirstOrDefaultAsync(s => s.MaSV == maSV);
            if (sv == null) 
                return Content("Lỗi: Không tìm thấy Sinh viên trong DB!");

            string maPhong = sv.MaPhong ?? "101";
            decimal giaPhongChuan = sv.Phong?.GiaPhong ?? 650000m;
            decimal donGiaDienChuan = 2500m;
            int csCu = 100;
            int csMoi = 155;
            decimal tongTienDien = (csMoi - csCu) * donGiaDienChuan;

            var fakeHoaDons = new List<HoaDon>
            {
                new HoaDon {
                    MaHoaDon = "HD_" + DateTime.Now.ToString("HHmmss") + "1",
                    LoaiHoaDon = "Tiền phòng",
                    DonGia = giaPhongChuan,
                    TongTien = giaPhongChuan,
                    TrangThai = "Chưa thanh toán",
                    HinhThucThanhToan = "Chưa có",
                    NgayLap = DateTime.Now,
                    MaSV = maSV,
                    MaPhong = maPhong
                },
                new HoaDon {
                    MaHoaDon = "HD_" + DateTime.Now.ToString("HHmmss") + "2",
                    LoaiHoaDon = "Điện nước",
                    ChiSoCu = csCu,
                    ChiSoMoi = csMoi,
                    DonGia = donGiaDienChuan,
                    TongTien = tongTienDien,
                    TrangThai = "Chưa thanh toán",
                    HinhThucThanhToan = "Chưa có",
                    NgayLap = DateTime.Now.AddDays(-2),
                    MaSV = maSV,
                    MaPhong = maPhong
                },
                new HoaDon {
                    MaHoaDon = "HD_" + DateTime.Now.ToString("HHmmss") + "3",
                    LoaiHoaDon = "Tiền phòng",
                    DonGia = giaPhongChuan,
                    TongTien = giaPhongChuan,
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

        public async Task<IActionResult> QuanLyHoaDonAdmin(string? searchString, string? loaiHoaDon)
        {
            if (EnsureAdminSession(nameof(QuanLyHoaDonAdmin)) is { } redirect)
                return redirect;

            ViewData["CurrentSearch"] = searchString;
            ViewData["CurrentLoai"] = loaiHoaDon;

            // Đếm tổng số hóa đơn đã thu để hiển thị badge nút "Lịch sử thu tiền"
            ViewBag.CountDaThu = await _context.HoaDons.CountAsync(h => h.TrangThai == "Đã thanh toán");

            // Mặc định chỉ lấy các hóa đơn có TrangThai == "Chưa thanh toán"
            var query = _context.HoaDons
                .Include(h => h.SinhVien)
                .Include(h => h.Phong)
                .Where(h => h.TrangThai == "Chưa thanh toán")
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                string searchLower = searchString.Trim().ToLower();
                query = query.Where(h => 
                    h.MaHoaDon.ToLower().Contains(searchLower) ||
                    h.MaSV.ToLower().Contains(searchLower) ||
                    (h.SinhVien != null && h.SinhVien.HoTen.ToLower().Contains(searchLower)) ||
                    h.MaPhong.ToLower().Contains(searchLower) ||
                    (h.Phong != null && h.Phong.MaPhong.ToLower().Contains(searchLower))
                );
            }

            if (!string.IsNullOrEmpty(loaiHoaDon) && loaiHoaDon != "Tất cả")
            {
                query = query.Where(h => h.LoaiHoaDon == loaiHoaDon);
            }

            var hoaDons = await query
                .OrderByDescending(h => h.NgayLap)
                .ToListAsync();

            ViewBag.CountCanThu = hoaDons.Count;
            return View(hoaDons);
        }

        [HttpGet]
        public async Task<IActionResult> LichSuHoaDonAdmin(string? searchString, string? loaiHoaDon)
        {
            if (EnsureAdminSession(nameof(LichSuHoaDonAdmin)) is { } redirect)
                return redirect;

            ViewData["CurrentSearch"] = searchString;
            ViewData["CurrentLoai"] = loaiHoaDon;

            // Đếm tổng số hóa đơn chưa thu để hiển thị badge nút quay lại
            ViewBag.CountCanThu = await _context.HoaDons.CountAsync(h => h.TrangThai == "Chưa thanh toán");
            ViewBag.CountDaThu = await _context.HoaDons.CountAsync(h => h.TrangThai == "Đã thanh toán");

            // Lấy danh sách các hóa đơn có TrangThai == "Đã thanh toán"
            var query = _context.HoaDons
                .Include(h => h.SinhVien)
                .Include(h => h.Phong)
                .Where(h => h.TrangThai == "Đã thanh toán")
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                string searchLower = searchString.Trim().ToLower();
                query = query.Where(h => 
                    h.MaHoaDon.ToLower().Contains(searchLower) ||
                    h.MaSV.ToLower().Contains(searchLower) ||
                    (h.SinhVien != null && h.SinhVien.HoTen.ToLower().Contains(searchLower)) ||
                    h.MaPhong.ToLower().Contains(searchLower) ||
                    (h.Phong != null && h.Phong.MaPhong.ToLower().Contains(searchLower))
                );
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
        public async Task<IActionResult> CapNhatTrangThaiHoaDon(string maHoaDon, string trangThaiMoi, string? returnUrl = null)
        {
            if (EnsureAdminSession(nameof(CapNhatTrangThaiHoaDon)) is { } redirect)
                return redirect;

            var hd = await _context.HoaDons.FirstOrDefaultAsync(h => h.MaHoaDon == maHoaDon);
            if (hd != null)
            {
                hd.TrangThai = trangThaiMoi;
                if (trangThaiMoi == "Đã thanh toán")
                {
                    hd.HinhThucThanhToan = HttpContext.Session.GetString("VaiTro") == "KeToan" 
                        ? "Tiền mặt (Kế toán thu)" 
                        : "Tiền mặt (Admin thu)";
                    TempData["SuccessMessage"] = $"Đã thu tiền hóa đơn {maHoaDon} và chuyển vào Lịch sử thu tiền!";
                }
                else
                {
                    hd.HinhThucThanhToan = "Chưa có";
                    TempData["SuccessMessage"] = $"Đã chuyển hóa đơn {maHoaDon} về danh sách cần thu!";
                }
                await _context.SaveChangesAsync();
            }
            else
            {
                TempData["ErrorMessage"] = $"Không tìm thấy hóa đơn {maHoaDon}!";
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(QuanLyHoaDonAdmin));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaHoaDon(string maHoaDon, string? returnUrl = null)
        {
            if (EnsureAdminSession(nameof(XoaHoaDon)) is { } redirect)
                return redirect;

            var hd = await _context.HoaDons.FirstOrDefaultAsync(h => h.MaHoaDon == maHoaDon);
            if (hd != null)
            {
                _context.HoaDons.Remove(hd);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã xóa thành công hóa đơn {maHoaDon}!";
            }
            else
            {
                TempData["ErrorMessage"] = $"Không tìm thấy hóa đơn {maHoaDon} để xóa!";
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(LichSuHoaDonAdmin));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaNhieuHoaDon(List<string> selectedIds, string? returnUrl = null)
        {
            if (EnsureAdminSession(nameof(XoaNhieuHoaDon)) is { } redirect)
                return redirect;

            if (selectedIds == null || !selectedIds.Any())
            {
                TempData["ErrorMessage"] = "Vui lòng chọn ít nhất một hóa đơn để xóa!";
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);
                return RedirectToAction(nameof(LichSuHoaDonAdmin));
            }

            var itemsToDelete = await _context.HoaDons
                .Where(h => selectedIds.Contains(h.MaHoaDon))
                .ToListAsync();

            int count = itemsToDelete.Count;
            if (count > 0)
            {
                _context.HoaDons.RemoveRange(itemsToDelete);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã xóa thành công {count} giao dịch đã chọn!";
            }
            else
            {
                TempData["ErrorMessage"] = "Không tìm thấy các hóa đơn đã chọn trong hệ thống!";
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(LichSuHoaDonAdmin));
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
                // Đơn giá tiền phòng tính theo đầu người của sinh viên đó theo biểu phí phòng
                donGia = donGiaTuNhap ?? (sv.Phong?.GiaPhong ?? 650000m);
                tongTien = donGia;
            }
            else if (loaiHoaDon == "Điện nước")
            {
                donGia = donGiaTuNhap ?? 2500m; // Đơn giá điện sinh hoạt chuẩn 2.500 VNĐ / kWh
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

        [AdminAuthorize(OnlyAdmin = true)]
        public async Task<IActionResult> QuanLySuCoAdmin()
        {
            if (EnsureAdminSession(nameof(QuanLySuCoAdmin)) is { } redirect)
                return redirect;

            var allSuCos = await _context.BaoCaoSuCos
                .Include(b => b.SinhVien)
                .Include(b => b.Phong)
                .OrderByDescending(b => b.NgayBaoCao)
                .ToListAsync();

            var dangXuLy = allSuCos.Where(b => b.TrangThai != "Đã xử lý" && b.TrangThai != "Từ chối").ToList();
            var lichSu = allSuCos.Where(b => b.TrangThai == "Đã xử lý" || b.TrangThai == "Từ chối").ToList();

            ViewBag.DangXuLy = dangXuLy;
            ViewBag.LichSu = lichSu;
            ViewBag.CountDangXuLy = dangXuLy.Count;
            ViewBag.CountLichSu = lichSu.Count;

            return View(dangXuLy);
        }

        [HttpPost]
        [AdminAuthorize(OnlyAdmin = true)]
        public async Task<IActionResult> CapNhatTrangThaiSuCo(string maSuCo, string trangThaiMoi)
        {
            if (EnsureAdminSession(nameof(CapNhatTrangThaiSuCo)) is { } redirect)
                return redirect;

            var sc = await _context.BaoCaoSuCos.FirstOrDefaultAsync(b => b.MaSuCo == maSuCo);
            if (sc != null)
            {
                sc.TrangThai = trangThaiMoi;
                await _context.SaveChangesAsync();

                if (trangThaiMoi == "Đã xử lý" || trangThaiMoi == "Từ chối")
                {
                    TempData["SuccessMessage"] = $"Đã cập nhật '{maSuCo}' thành '{trangThaiMoi}' và tự động chuyển sang Quản lý Lịch sử!";
                }
                else
                {
                    TempData["SuccessMessage"] = $"Cập nhật trạng thái sự cố {maSuCo} thành '{trangThaiMoi}' thành công!";
                }
            }
            else
            {
                TempData["ErrorMessage"] = $"Không tìm thấy sự cố {maSuCo}!";
            }

            return RedirectToAction(nameof(QuanLySuCoAdmin));
        }

        // ==========================================
        // 5.5. QUẢN LÝ LỊCH SỬ BÁO CÁO & HỖ TRỢ SV (XÓA ĐƠN LẺ, XÓA NHIỀU, XÓA TẤT CẢ)
        // ==========================================
        [HttpPost]
        [AdminAuthorize(OnlyAdmin = true)]
        public async Task<IActionResult> XoaSuCo(string maSuCo)
        {
            if (EnsureAdminSession(nameof(XoaSuCo)) is { } redirect)
                return redirect;

            if (string.IsNullOrWhiteSpace(maSuCo))
            {
                TempData["ErrorMessage"] = "Mã sự cố không hợp lệ!";
                return RedirectToAction(nameof(QuanLySuCoAdmin));
            }

            var item = await _context.BaoCaoSuCos.FirstOrDefaultAsync(b => b.MaSuCo == maSuCo);
            if (item != null)
            {
                _context.BaoCaoSuCos.Remove(item);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã xóa yêu cầu {maSuCo} khỏi hệ thống thành công!";
            }
            else
            {
                TempData["ErrorMessage"] = $"Không tìm thấy yêu cầu {maSuCo} để xóa!";
            }

            return RedirectToAction(nameof(QuanLySuCoAdmin));
        }

        [HttpPost]
        [AdminAuthorize(OnlyAdmin = true)]
        public async Task<IActionResult> XoaNhieuSuCo(List<string> maSuCos)
        {
            if (EnsureAdminSession(nameof(XoaNhieuSuCo)) is { } redirect)
                return redirect;

            if (maSuCos == null || !maSuCos.Any())
            {
                TempData["ErrorMessage"] = "Chưa có mục nào được chọn để xóa!";
                return RedirectToAction(nameof(QuanLySuCoAdmin));
            }

            var items = await _context.BaoCaoSuCos.Where(b => maSuCos.Contains(b.MaSuCo)).ToListAsync();
            if (items.Any())
            {
                int count = items.Count;
                _context.BaoCaoSuCos.RemoveRange(items);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã xóa thành công {count} yêu cầu khỏi phần lịch sử!";
            }
            else
            {
                TempData["ErrorMessage"] = "Không tìm thấy yêu cầu phù hợp để xóa!";
            }

            return RedirectToAction(nameof(QuanLySuCoAdmin));
        }

        [HttpPost]
        [AdminAuthorize(OnlyAdmin = true)]
        public async Task<IActionResult> XoaTatCaLichSu()
        {
            if (EnsureAdminSession(nameof(XoaTatCaLichSu)) is { } redirect)
                return redirect;

            var items = await _context.BaoCaoSuCos
                .Where(b => b.TrangThai == "Đã xử lý" || b.TrangThai == "Từ chối")
                .ToListAsync();

            if (items.Any())
            {
                int count = items.Count;
                _context.BaoCaoSuCos.RemoveRange(items);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã xóa sạch toàn bộ {count} yêu cầu trong phần Lịch sử!";
            }
            else
            {
                TempData["SuccessMessage"] = "Hiện không có yêu cầu nào trong phần Lịch sử để xóa.";
            }

            return RedirectToAction(nameof(QuanLySuCoAdmin));
        }

        // ==========================================
        // 6. AI TÓM TẮT SỰ CỐ (TÍNH NĂNG MỚI ĐỘC LẬP)
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> TomTatSuCoAI()
        {
            if (HttpContext.Session.GetString("Admin_DangNhap") != "true")
            {
                return Json(new { success = false, summary = "Lỗi: Không có quyền truy cập." });
            }

            try
            {
                var suCos = await _context.BaoCaoSuCos
                    .Include(b => b.Phong)
                    .Where(b => b.TrangThai == "Chờ xử lý")
                    .Select(b => new { b.MaSuCo, b.Phong.MaPhong, b.TenSuCo, b.MoTa, b.NgayBaoCao })
                    .ToListAsync();

                if (suCos == null || !suCos.Any())
                {
                    return Json(new { success = true, summary = "Hiện không có sự cố nào đang chờ xử lý." });
                }

                string jsonData = System.Text.Json.JsonSerializer.Serialize(suCos);
                string prompt = AIPrompts.BuildIncidentSummaryPrompt(jsonData);
                string modelName = _configuration["Ollama:Model"] ?? "llama3";

                var payload = new
                {
                    model = modelName,
                    messages = new[]
                    {
                        new { role = "system", content = AIPrompts.IncidentSummarySystemPrompt },
                        new { role = "user", content = prompt }
                    },
                    stream = false
                };

                try
                {
                    string baseUrl = _configuration["Ollama:BaseUrl"] ?? "http://localhost:11434/api/chat";
                    var httpClient = _httpClientFactory.CreateClient("Ollama");
                    httpClient.Timeout = TimeSpan.FromSeconds(8);

                    var content = new System.Net.Http.StringContent(System.Text.Json.JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
                    var res = await httpClient.PostAsync(baseUrl, content);

                    if (res.IsSuccessStatusCode)
                    {
                        var json = await res.Content.ReadAsStringAsync();
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        string? aiResponse = doc.RootElement.GetProperty("message").GetProperty("content").GetString();
                        if (!string.IsNullOrWhiteSpace(aiResponse))
                            return Json(new { success = true, summary = aiResponse });
                    }
                }
                catch { }

                // Fallback tổng hợp thông minh khi dịch vụ Ollama cục bộ chưa bật
                var nhomDien = suCos.Where(s => s.TenSuCo.Contains("điện", StringComparison.OrdinalIgnoreCase) || s.TenSuCo.Contains("đèn", StringComparison.OrdinalIgnoreCase) || s.TenSuCo.Contains("quạt", StringComparison.OrdinalIgnoreCase) || s.TenSuCo.Contains("điều hòa", StringComparison.OrdinalIgnoreCase)).ToList();
                var nhomNuoc = suCos.Where(s => s.TenSuCo.Contains("nước", StringComparison.OrdinalIgnoreCase) || s.TenSuCo.Contains("vòi", StringComparison.OrdinalIgnoreCase) || s.TenSuCo.Contains("ống", StringComparison.OrdinalIgnoreCase)).ToList();
                var nhomKhac = suCos.Except(nhomDien).Except(nhomNuoc).ToList();

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"📊 BÁO CÁO TỔNG HỢP SỰ CỐ ĐANG CHỜ XỬ LÝ ({suCos.Count} sự cố):");
                sb.AppendLine();
                sb.AppendLine($"1. Phân loại theo nhóm kỹ thuật:");
                sb.AppendLine($"   • Nhóm Điện / Thiết bị điện ({nhomDien.Count} sự cố): " + (nhomDien.Any() ? string.Join(", ", nhomDien.Select(x => $"P.{x.MaPhong} ({x.TenSuCo})")) : "Không có"));
                sb.AppendLine($"   • Nhóm Cấp thoát nước ({nhomNuoc.Count} sự cố): " + (nhomNuoc.Any() ? string.Join(", ", nhomNuoc.Select(x => $"P.{x.MaPhong} ({x.TenSuCo})")) : "Không có"));
                sb.AppendLine($"   • Nhóm Nội thất / Khác ({nhomKhac.Count} sự cố): " + (nhomKhac.Any() ? string.Join(", ", nhomKhac.Select(x => $"P.{x.MaPhong} ({x.TenSuCo})")) : "Không có"));
                sb.AppendLine();
                sb.AppendLine("2. Đề xuất thứ tự ưu tiên xử lý:");
                if (nhomDien.Any())
                    sb.AppendLine("   • Ưu tiên 1 (Khẩn cấp): Cử thợ điện kiểm tra ngay các phòng " + string.Join(", ", nhomDien.Select(x => "P." + x.MaPhong).Distinct()) + " để đảm bảo an toàn điện và sinh hoạt.");
                if (nhomNuoc.Any())
                    sb.AppendLine("   • Ưu tiên 2 (Quan trọng): Khắc phục sự cố nước tại " + string.Join(", ", nhomNuoc.Select(x => "P." + x.MaPhong).Distinct()) + " tránh thất thoát nước.");
                if (nhomKhac.Any())
                    sb.AppendLine("   • Ưu tiên 3: Bảo trì khóa cửa / nội thất tại " + string.Join(", ", nhomKhac.Select(x => "P." + x.MaPhong).Distinct()) + ".");

                return Json(new { success = true, summary = sb.ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi gọi AI tóm tắt sự cố.");
                return Json(new { success = false, summary = "Lỗi hệ thống: " + ex.Message });
            }
        }
    }
}