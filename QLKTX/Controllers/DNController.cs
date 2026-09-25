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
            var account = await _context.TaiKhoans
                .FirstOrDefaultAsync(a => a.TenDangNhap == Username && a.MatKhau == Password);

            if (account != null)
            {
                if (!string.IsNullOrEmpty(account.MaSV))
                {
                    HttpContext.Session.SetString("MaSV_DangNhap", account.MaSV);
                    return RedirectToAction("Index", "Portal");
                }
                else
                {
                    HttpContext.Session.SetString("Admin_DangNhap", "true");
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

        // GET: /DN/LienheQuanly
        public IActionResult LienheQuanly()
        {
            return View();
        }
    }
}