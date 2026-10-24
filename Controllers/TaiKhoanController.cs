using System.Linq;
using System.Web.Mvc;
using Phân_Tích_Thiết_Kế_Website_QL_Sân_Bóng.Models;

namespace Phân_Tích_Thiết_Kế_Website_QL_Sân_Bóng.Controllers
{
    public class TaiKhoanController : Controller
    {
        private QLSanBongEntities db = new QLSanBongEntities();


        // =====================================================
        // ĐĂNG NHẬP - GET
        // =====================================================

        [HttpGet]
        public ActionResult DangNhap()
        {
            return View();
        }


        // =====================================================
        // ĐĂNG NHẬP - POST
        // =====================================================

        [HttpPost]
        public ActionResult DangNhap(string soDienThoai, string matKhau)
        {
            if (string.IsNullOrEmpty(soDienThoai) ||
                string.IsNullOrEmpty(matKhau))
            {
                ViewBag.ThongBao =
                    "Vui lòng nhập số điện thoại và mật khẩu.";

                return View();
            }


            var nguoiDung = db.NguoiDungs.FirstOrDefault(x =>
                x.SoDienThoai == soDienThoai &&
                x.MatKhau == matKhau);


            if (nguoiDung == null)
            {
                ViewBag.ThongBao =
                    "Số điện thoại hoặc mật khẩu không đúng.";

                return View();
            }


            if (nguoiDung.TrangThai != "Hoạt động")
            {
                ViewBag.ThongBao =
                    "Tài khoản hiện không hoạt động.";

                return View();
            }


            // Lưu thông tin đăng nhập vào Session

            Session["MaNguoiDung"] = nguoiDung.MaNguoiDung;

            Session["TenNguoiDung"] = nguoiDung.HoTen;

            Session["VaiTro"] = nguoiDung.VaiTro;


            // ==========================
            // PHÂN QUYỀN
            // ==========================

            if (nguoiDung.VaiTro == "Admin")
            {
                return RedirectToAction("Index", "Home");
            }


            if (nguoiDung.VaiTro == "NhanVien")
            {
                return RedirectToAction("Index", "Home");
            }


            if (nguoiDung.VaiTro == "KhachHang")
            {
                return RedirectToAction("Index", "Home");
            }


            // Nếu tài khoản không có quyền

            Session.Clear();

            ViewBag.ThongBao =
                "Tài khoản chưa được phân quyền.";

            return View();
        }


        // =====================================================
        // ĐĂNG KÝ - GET
        // =====================================================

        [HttpGet]
        public ActionResult DangKy()
        {
            return View();
        }


        // =====================================================
        // ĐĂNG KÝ - POST
        // =====================================================

        [HttpPost]
        public ActionResult DangKy(
            string hoTen,
            string soDienThoai,
            string email,
            string matKhau)
        {
            if (string.IsNullOrEmpty(hoTen) ||
                string.IsNullOrEmpty(soDienThoai) ||
                string.IsNullOrEmpty(matKhau))
            {
                ViewBag.ThongBao =
                    "Vui lòng nhập đầy đủ thông tin.";

                return View();
            }


            // Kiểm tra số điện thoại đã tồn tại

            bool tonTai = db.NguoiDungs.Any(x =>
                x.SoDienThoai == soDienThoai);


            if (tonTai)
            {
                ViewBag.ThongBao =
                    "Số điện thoại đã được đăng ký.";

                return View();
            }


            // Tạo tài khoản mới

            NguoiDung nguoiDung = new NguoiDung();


            nguoiDung.HoTen = hoTen;

            nguoiDung.SoDienThoai = soDienThoai;

            nguoiDung.Email = email;

            nguoiDung.MatKhau = matKhau;

            // Tài khoản đăng ký mặc định là khách hàng

            nguoiDung.VaiTro = "KhachHang";

            nguoiDung.TrangThai = "Hoạt động";


            db.NguoiDungs.Add(nguoiDung);

            db.SaveChanges();


            return RedirectToAction("DangNhap");
        }


        // =====================================================
        // ĐĂNG XUẤT
        // =====================================================

        public ActionResult DangXuat()
        {
            Session.Clear();

            return RedirectToAction("DangNhap");
        }


        // =====================================================
        // XEM TÀI KHOẢN
        // URL: /TaiKhoan/TaiKhoan
        // =====================================================

        [RoleAuthorize(Roles = "Admin,NhanVien,KhachHang")]
        public ActionResult TaiKhoan()
        {
            // Kiểm tra đăng nhập

            if (Session["MaNguoiDung"] == null)
            {
                return RedirectToAction(
                    "DangNhap",
                    "TaiKhoan"
                );
            }


            // Lấy mã người dùng

            int MaNguoiDung =
                (int)Session["MaNguoiDung"];


            // Tìm thông tin người dùng

            var NguoiDung =
                db.NguoiDungs.Find(MaNguoiDung);


            // Không tìm thấy tài khoản

            if (NguoiDung == null)
            {
                Session.Clear();

                return RedirectToAction(
                    "DangNhap",
                    "TaiKhoan"
                );
            }


            // Trả thông tin sang View

            return View(NguoiDung);
        }


        // =====================================================
        // XEM THÔNG TIN
        // URL: /TaiKhoan/ThongTin
        // =====================================================

        [RoleAuthorize(Roles = "Admin,NhanVien,KhachHang")]
        public ActionResult ThongTin()
        {
            // Kiểm tra đăng nhập

            if (Session["MaNguoiDung"] == null)
            {
                return RedirectToAction(
                    "DangNhap",
                    "TaiKhoan"
                );
            }


            int MaNguoiDung =
                (int)Session["MaNguoiDung"];


            var NguoiDung =
                db.NguoiDungs.Find(MaNguoiDung);


            if (NguoiDung == null)
            {
                return RedirectToAction(
                    "DangNhap",
                    "TaiKhoan"
                );
            }


            return View(NguoiDung);
        }


        // =====================================================
        // DISPOSE
        // =====================================================

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}