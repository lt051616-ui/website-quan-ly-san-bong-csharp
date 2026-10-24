using System.Web;
using System.Web.Mvc;

namespace Phân_Tích_Thiết_Kế_Website_QL_Sân_Bóng
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
        }
    }
}
