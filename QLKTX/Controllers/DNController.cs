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
        public IActionResult Dangnhap()
        {
            return View();
        }

        // POST: /DN/Dangnhap
        [HttpPost]
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
                .FirstOrDefaultAsync(a => 
                    (a.TenDangNhap == Username || a.MaSV == Username) 
                    && a.MatKhau == Password);

            if (account != null)
            {
                if (!string.IsNullOrEmpty(account.MaSV))
                {
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
        public IActionResult ContactAdmin(ContactViewModel model)
        {
            TempData["Success"] = true;
            return View("LienheQuanly");
        }
    }
}
