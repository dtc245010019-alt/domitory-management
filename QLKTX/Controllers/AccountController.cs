using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLKTX.Models;

namespace QLKTX.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Account/Login
        public IActionResult Login()
        {
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            // Tìm tài khoản trong Database
            var account = await _context.TaiKhoans
                .FirstOrDefaultAsync(a => a.TenDangNhap == username && a.MatKhau == password);

            if (account != null)
            {
                

                // 1. NẾU LÀ SINH VIÊN (Cột MaSV có dữ liệu)
                if (!string.IsNullOrEmpty(account.MaSV))
                {
                    // Lưu mã SV vào Session
                    HttpContext.Session.SetString("MaSV_DangNhap", account.MaSV);

                    // ĐIỀU HƯỚNG VỀ TRANG CỦA SINH VIÊN
                    return RedirectToAction("Index", "Portal");
                }
                // 2. NẾU LÀ ADMIN (Cột MaSV bị null hoặc rỗng)
                else
                {
                    HttpContext.Session.SetString("Admin_DangNhap", "true");

                    // ĐIỀU HƯỚNG VỀ TRANG QUẢN LÝ
                    return RedirectToAction("Index", "Home");
                }
            }

            ViewBag.Error = "Tên đăng nhập hoặc mật khẩu không chính xác!";
            return View();
        }
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}