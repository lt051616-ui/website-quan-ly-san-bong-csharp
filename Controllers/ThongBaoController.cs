using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Phân_Tích_Thiết_Kế_Website_QL_Sân_Bóng.Models;

namespace Phân_Tích_Thiết_Kế_Website_QL_Sân_Bóng.Controllers
{
    public class ThongBaoController : Controller
    {
        private QLSanBongEntities db = new QLSanBongEntities();


        // =====================================================
        // DROPDOWN THÔNG BÁO TRÊN _LAYOUT
        //
        // Được gọi bởi:
        // @Html.Action("Dropdown", "ThongBao")
        //
        // Lấy:
        // - 5 thông báo mới nhất
        // - Số lượng thông báo chưa đọc
        // =====================================================
        [ChildActionOnly]
        public ActionResult Dropdown()
        {
            // Nếu chưa đăng nhập
            if (Session["MaNguoiDung"] == null)
            {
                ViewBag.SoChuaDoc = 0;

                return PartialView(
                    "~/Views/Shared/_ThongBaoDropdown.cshtml",
                    new List<ThongBao>()
                );
            }


            int maNguoiDung =
                Convert.ToInt32(Session["MaNguoiDung"]);


            // 5 thông báo mới nhất của tài khoản
            var danhSachThongBao = db.ThongBaos
                .Where(t =>
                    t.MaNguoiDung == maNguoiDung)
                .OrderByDescending(t =>
                    t.NgayTao)
                .Take(5)
                .ToList();


            // Đếm số thông báo chưa đọc
            int soChuaDoc = db.ThongBaos
                .Count(t =>
                    t.MaNguoiDung == maNguoiDung &&
                    t.DaDoc == false);


            ViewBag.SoChuaDoc = soChuaDoc;


            return PartialView(
                "~/Views/Shared/_ThongBaoDropdown.cshtml",
                danhSachThongBao
            );
        }



        // =====================================================
        // XEM TẤT CẢ THÔNG BÁO
        //
        // URL:
        // /ThongBao
        // hoặc
        // /ThongBao/Index
        // =====================================================
        [RoleAuthorize(Roles = "Admin,NhanVien,KhachHang")]
        public ActionResult Index()
        {
            // Kiểm tra đăng nhập
            if (Session["MaNguoiDung"] == null)
            {
                return RedirectToAction(
                    "DangNhap",
                    "TaiKhoan"
                );
            }


            int maNguoiDung =
                Convert.ToInt32(Session["MaNguoiDung"]);


            // Chỉ lấy thông báo của tài khoản đang đăng nhập
            var danhSachThongBao = db.ThongBaos
                .Where(t =>
                    t.MaNguoiDung == maNguoiDung)
                .OrderByDescending(t =>
                    t.NgayTao)
                .ToList();


            return View(danhSachThongBao);
        }



        // =====================================================
        // XEM MỘT THÔNG BÁO
        //
        // Khi bấm vào thông báo:
        // 1. Đánh dấu đã đọc
        // 2. Chuyển tới trang liên quan
        // =====================================================
        [RoleAuthorize(Roles = "Admin,NhanVien,KhachHang")]
        public ActionResult Xem(int id)
        {
            if (Session["MaNguoiDung"] == null)
            {
                return RedirectToAction(
                    "DangNhap",
                    "TaiKhoan"
                );
            }


            int maNguoiDung =
                Convert.ToInt32(Session["MaNguoiDung"]);


            // Quan trọng:
            // Phải kiểm tra MaNguoiDung để người khác
            // không xem được thông báo của tài khoản khác.
            var thongBao = db.ThongBaos
                .FirstOrDefault(t =>
                    t.MaThongBao == id &&
                    t.MaNguoiDung == maNguoiDung);


            // Không tìm thấy
            if (thongBao == null)
            {
                TempData["ThongBaoError"] =
                    "Không tìm thấy thông báo.";

                return RedirectToAction("Index");
            }


            // =============================================
            // ĐÁNH DẤU ĐÃ ĐỌC
            // =============================================
            if (thongBao.DaDoc == false)
            {
                thongBao.DaDoc = true;

                db.SaveChanges();
            }


            // =============================================
            // NẾU CÓ ĐƯỜNG DẪN LIÊN QUAN
            // THÌ CHUYỂN TỚI ĐÓ
            // =============================================
            if (!string.IsNullOrWhiteSpace(
                    thongBao.DuongDan))
            {
                // Chỉ cho phép URL nội bộ website
                if (Url.IsLocalUrl(
                    thongBao.DuongDan))
                {
                    return Redirect(
                        thongBao.DuongDan
                    );
                }
            }


            // Không có đường dẫn
            // thì quay về trang danh sách thông báo
            return RedirectToAction("Index");
        }



        // =====================================================
        // ĐÁNH DẤU TẤT CẢ ĐÃ ĐỌC
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize(Roles = "Admin,NhanVien,KhachHang")]
        public ActionResult DanhDauTatCaDaDoc()
        {
            if (Session["MaNguoiDung"] == null)
            {
                return RedirectToAction(
                    "DangNhap",
                    "TaiKhoan"
                );
            }


            int maNguoiDung =
                Convert.ToInt32(Session["MaNguoiDung"]);


            // Lấy tất cả thông báo chưa đọc
            // của chính tài khoản đang đăng nhập
            var danhSachChuaDoc = db.ThongBaos
                .Where(t =>
                    t.MaNguoiDung == maNguoiDung &&
                    t.DaDoc == false)
                .ToList();


            foreach (var thongBao in danhSachChuaDoc)
            {
                thongBao.DaDoc = true;
            }


            db.SaveChanges();


            TempData["ThongBaoSuccess"] =
                "Đã đánh dấu tất cả thông báo là đã đọc.";


            return RedirectToAction("Index");
        }



        // =====================================================
        // ĐÁNH DẤU MỘT THÔNG BÁO ĐÃ ĐỌC
        //
        // Có thể dùng sau này nếu muốn có nút
        // "Đánh dấu đã đọc" mà không mở thông báo.
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize(Roles = "Admin,NhanVien,KhachHang")]
        public ActionResult DanhDauDaDoc(int id)
        {
            if (Session["MaNguoiDung"] == null)
            {
                return RedirectToAction(
                    "DangNhap",
                    "TaiKhoan"
                );
            }


            int maNguoiDung =
                Convert.ToInt32(Session["MaNguoiDung"]);


            var thongBao = db.ThongBaos
                .FirstOrDefault(t =>
                    t.MaThongBao == id &&
                    t.MaNguoiDung == maNguoiDung);


            if (thongBao != null)
            {
                thongBao.DaDoc = true;

                db.SaveChanges();
            }


            return RedirectToAction("Index");
        }



        // =====================================================
        // XÓA MỘT THÔNG BÁO
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize(Roles = "Admin,NhanVien,KhachHang")]
        public ActionResult Xoa(int id)
        {
            if (Session["MaNguoiDung"] == null)
            {
                return RedirectToAction(
                    "DangNhap",
                    "TaiKhoan"
                );
            }


            int maNguoiDung =
                Convert.ToInt32(Session["MaNguoiDung"]);


            // Chỉ được xóa thông báo của chính mình
            var thongBao = db.ThongBaos
                .FirstOrDefault(t =>
                    t.MaThongBao == id &&
                    t.MaNguoiDung == maNguoiDung);


            if (thongBao == null)
            {
                TempData["ThongBaoError"] =
                    "Không tìm thấy thông báo.";

                return RedirectToAction("Index");
            }


            db.ThongBaos.Remove(thongBao);

            db.SaveChanges();


            TempData["ThongBaoSuccess"] =
                "Đã xóa thông báo.";


            return RedirectToAction("Index");
        }



        // =====================================================
        // GIẢI PHÓNG DATABASE
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