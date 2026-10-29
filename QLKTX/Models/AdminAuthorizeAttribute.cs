using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace QLKTX.Models
{
    /// <summary>
    /// Attribute kiểm tra quyền truy cập của Ban Quản lý (Admin) và Phòng Kế toán (KeToan).
    /// - Nếu chưa đăng nhập: chuyển hướng về /DN/Dangnhap.
    /// - Nếu OnlyAdmin = true: chặn tài khoản KeToan truy cập vào các phân hệ kỹ thuật/kỷ luật của Admin.
    /// </summary>
    public class AdminAuthorizeAttribute : ActionFilterAttribute
    {
        public bool OnlyAdmin { get; set; } = false;

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ActionDescriptor.EndpointMetadata.OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>().Any())
            {
                base.OnActionExecuting(context);
                return;
            }

            var session = context.HttpContext.Session;
            var adminSession = session.GetString("Admin_DangNhap");
            if (adminSession != "true")
            {
                context.Result = new RedirectToActionResult("Dangnhap", "DN", null);
                return;
            }

            if (OnlyAdmin)
            {
                var vaiTro = session.GetString("VaiTro") ?? "Admin";
                if (string.Equals(vaiTro, "KeToan", StringComparison.OrdinalIgnoreCase))
                {
                    var factory = context.HttpContext.RequestServices.GetService<ITempDataDictionaryFactory>();
                    if (factory != null)
                    {
                        var tempData = factory.GetTempData(context.HttpContext);
                        tempData["AlertMessage"] = "Phân quyền: Tài khoản Kế toán chỉ được phép truy cập phân hệ Tài chính, Hóa đơn và Hợp đồng!";
                    }
                    context.Result = new RedirectToActionResult("Index", "Home", null);
                    return;
                }
            }

            base.OnActionExecuting(context);
        }
    }
}
