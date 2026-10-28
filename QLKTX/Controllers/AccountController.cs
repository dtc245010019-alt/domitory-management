using Microsoft.AspNetCore.Mvc;

namespace QLKTX.Controllers
{
    /// <summary>
    /// Controller chuyển hướng tương thích ngược về phân hệ xác thực chuẩn DNController.
    /// Đảm bảo phiên làm việc được thiết lập đầy đủ VaiTro (Admin, KeToan, SinhVien).
    /// </summary>
    public class AccountController : Controller
    {
        public IActionResult Login() => RedirectToAction("Dangnhap", "DN");
        public IActionResult Logout() => RedirectToAction("Dangxuat", "DN");
    }
}
