using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Phân_Tích_Thiết_Kế_Website_QL_Sân_Bóng.Controllers
{
    public class RoleAuthorizeAttribute : AuthorizeAttribute
    {
        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            if (httpContext.Session["MaNguoiDung"] == null)
            {
                return false;
            }

            string vaiTro = httpContext.Session["VaiTro"] as string;

            if (string.IsNullOrEmpty(vaiTro))
            {
                return false;
            }

            string[] danhSachVaiTro = Roles
                .Split(',')
                .Select(x => x.Trim())
                .ToArray();

            return danhSachVaiTro.Contains(vaiTro);
        }

        protected override void HandleUnauthorizedRequest(
            AuthorizationContext filterContext)
        {
            if (filterContext.HttpContext.Session["MaNguoiDung"] == null)
            {
                filterContext.Result = new RedirectToRouteResult(
                    new System.Web.Routing.RouteValueDictionary
                    {
                        { "controller", "TaiKhoan" },
                        { "action", "DangNhap" }
                    });
            }
            else
            {
                filterContext.Result =
                    new HttpUnauthorizedResult();
            }
        }
    }
}