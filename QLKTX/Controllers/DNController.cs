using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLKTX.Models;
using Microsoft.AspNetCore.Http;

namespace QLKTX.Controllers
{
    public class DNController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DNController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /DN/Dangnhap
        public IActionResult Index() => RedirectToAction(nameof(Dangnhap));

        public IActionResult Dangnhap()
        {
            return View();
        }

        // POST: /DN/Dangnhap
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Dangnhap(string Username, string Password)
        {
            Username = Username?.Trim();
            Password = Password?.Trim();

            if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
            {
                ModelState.AddModelError(string.Empty, "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu!");
                return View();
            }

            var account = await _context.TaiKhoans
                .FirstOrDefaultAsync(a => a.TenDangNhap == Username || a.MaSV == Username);

            if (account != null && PasswordHelper.VerifyPassword(account.MatKhau, Password))
            {
                if (!string.IsNullOrEmpty(account.MaSV))
                {
                    // KIỂM SOÁT TRUY CẬP: Ngăn chặn sinh viên đã rời đi hoặc bị đình chỉ truy cập
                    var sv = await _context.SinhViens.FirstOrDefaultAsync(s => s.MaSV == account.MaSV);
                    if (sv != null && (sv.TinhTrangLuuTru == "Đã rời đi" || sv.TinhTrangLuuTru == "Đình chỉ"))
                    {
                        ModelState.AddModelError(string.Empty, $"Tài khoản không thể truy cập do hồ sơ sinh viên ở trạng thái '{sv.TinhTrangLuuTru}'. Vui lòng liên hệ Ban Quản lý KTX!");
                        return View();
                    }

                    HttpContext.Session.SetString("MaSV_DangNhap", account.MaSV);
                    HttpContext.Session.SetString("VaiTro", "SinhVien");
                    return RedirectToAction("Index", "Portal");
                }
                else
                {
                    HttpContext.Session.SetString("Admin_DangNhap", "true");
                    HttpContext.Session.SetString("VaiTro", string.IsNullOrEmpty(account.Quyen) ? "Admin" : account.Quyen);
                    HttpContext.Session.SetString("TenDangNhap", account.TenDangNhap);
                    return RedirectToAction("Index", "Home");
                }
            }

            ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không chính xác!");
            return View();
        }

        // GET: /DN/Dangxuat
        public IActionResult Dangxuat()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Dangnhap");
        }

        // GET: /DN/QuenMK
        public IActionResult QuenMK()
        {
            return View();
        }

        // POST: /DN/ForgotPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!string.IsNullOrWhiteSpace(model?.Email))
            {
                TempData["Success"] = true;
                TempData["Email"] = model.Email.Trim();
            }
            return View("QuenMK", model);
        }

        // GET: /DN/LienheQuanly
        public IActionResult LienheQuanly()
        {
            return View();
        }

        // POST: /DN/ContactAdmin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ContactAdmin(ContactViewModel model)
        {
            TempData["Success"] = true;
            return View("LienheQuanly");
        }
    }
}
