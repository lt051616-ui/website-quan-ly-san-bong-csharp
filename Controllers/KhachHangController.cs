using System;
using System.Linq;
using System.Web.Mvc;
using Phân_Tích_Thiết_Kế_Website_QL_Sân_Bóng.Models;

namespace Phân_Tích_Thiết_Kế_Website_QL_Sân_Bóng.Controllers
{
    public class KhachHangController : Controller
    {
        private QLSanBongEntities db = new QLSanBongEntities();

        // =====================================================
        // KIỂM TRA QUYỀN ADMIN / NHÂN VIÊN
        // =====================================================
        private bool KiemTraQuyen()
        {
            if (Session["MaNguoiDung"] == null)
            {
                return false;
            }

            string vaiTro = Session["VaiTro"] as string;

            return vaiTro == "Admin" || vaiTro == "NhanVien";
        }

        // =====================================================
        // DANH SÁCH KHÁCH HÀNG
        // =====================================================
        public ActionResult Index(string tuKhoa, string trangThai)
        {
            if (!KiemTraQuyen())
            {
                TempData["Error"] = "Bạn không có quyền truy cập chức năng này!";
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            var dsKhachHang = db.NguoiDungs
                .Where(x => x.VaiTro == "KhachHang");

            // TÌM KIẾM
            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                tuKhoa = tuKhoa.Trim();

                dsKhachHang = dsKhachHang.Where(x =>
                    x.HoTen.Contains(tuKhoa) ||
                    x.SoDienThoai.Contains(tuKhoa) ||
                    (x.Email != null && x.Email.Contains(tuKhoa))
                );
            }

            // LỌC TRẠNG THÁI
            if (!string.IsNullOrWhiteSpace(trangThai))
            {
                dsKhachHang = dsKhachHang.Where(x =>
                    x.TrangThai == trangThai);
            }

            ViewBag.TuKhoa = tuKhoa;
            ViewBag.TrangThai = trangThai;

            return View(
                dsKhachHang
                    .OrderByDescending(x => x.MaNguoiDung)
                    .ToList()
            );
        }

        // =====================================================
        // THÊM KHÁCH HÀNG
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ThemMoi(
            string hoTen,
            string soDienThoai,
            string email)
        {
            if (!KiemTraQuyen())
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            if (string.IsNullOrWhiteSpace(hoTen))
            {
                TempData["Error"] = "Vui lòng nhập họ tên khách hàng!";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(soDienThoai))
            {
                TempData["Error"] = "Vui lòng nhập số điện thoại!";
                return RedirectToAction("Index");
            }

            soDienThoai = soDienThoai.Trim();

            if (db.NguoiDungs.Any(x => x.SoDienThoai == soDienThoai))
            {
                TempData["Error"] = "Số điện thoại này đã tồn tại trên hệ thống!";
                return RedirectToAction("Index");
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                email = email.Trim();

                if (db.NguoiDungs.Any(x => x.Email == email))
                {
                    TempData["Error"] = "Email này đã tồn tại trên hệ thống!";
                    return RedirectToAction("Index");
                }
            }

            NguoiDung khachHang = new NguoiDung();
            khachHang.HoTen = hoTen.Trim();
            khachHang.SoDienThoai = soDienThoai;
            khachHang.Email = email;
            khachHang.MatKhau = "123456";
            khachHang.VaiTro = "KhachHang";
            khachHang.NgayTao = DateTime.Now;
            khachHang.TrangThai = "Hoạt động";

            db.NguoiDungs.Add(khachHang);
            db.SaveChanges();

            TempData["Success"] = "Thêm khách hàng mới thành công!";

            return RedirectToAction("Index");
        }

        // =====================================================
        // XEM CHI TIẾT KHÁCH HÀNG
        // =====================================================
        [HttpGet]
        public ActionResult ChiTiet(int id)
        {
            if (!KiemTraQuyen())
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            var khachHang = db.NguoiDungs
                .FirstOrDefault(x =>
                    x.MaNguoiDung == id &&
                    x.VaiTro == "KhachHang"
                );

            if (khachHang == null)
            {
                TempData["Error"] = "Không tìm thấy khách hàng!";
                return RedirectToAction("Index");
            }

            return View(khachHang);
        }

        // =====================================================
        // SỬA KHÁCH HÀNG - GET
        // =====================================================
        [HttpGet]
        public ActionResult Sua(int id)
        {
            if (!KiemTraQuyen())
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            var khachHang = db.NguoiDungs
                .FirstOrDefault(x =>
                    x.MaNguoiDung == id &&
                    x.VaiTro == "KhachHang"
                );

            if (khachHang == null)
            {
                TempData["Error"] = "Không tìm thấy khách hàng!";
                return RedirectToAction("Index");
            }

            return View(khachHang);
        }

        // =====================================================
        // SỬA KHÁCH HÀNG - POST
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Sua(
            int id,
            string hoTen,
            string soDienThoai,
            string email,
            string trangThai)
        {
            if (!KiemTraQuyen())
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            var khachHang = db.NguoiDungs
                .FirstOrDefault(x =>
                    x.MaNguoiDung == id &&
                    x.VaiTro == "KhachHang"
                );

            if (khachHang == null)
            {
                TempData["Error"] = "Không tìm thấy khách hàng!";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(hoTen))
            {
                TempData["Error"] = "Họ tên không được để trống!";
                return RedirectToAction("Sua", new { id = id });
            }

            if (string.IsNullOrWhiteSpace(soDienThoai))
            {
                TempData["Error"] = "Số điện thoại không được để trống!";
                return RedirectToAction("Sua", new { id = id });
            }

            soDienThoai = soDienThoai.Trim();

            bool trungSDT = db.NguoiDungs.Any(x =>
                x.SoDienThoai == soDienThoai &&
                x.MaNguoiDung != id
            );

            if (trungSDT)
            {
                TempData["Error"] = "Số điện thoại này đã được sử dụng!";
                return RedirectToAction("Sua", new { id = id });
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                email = email.Trim();

                bool trungEmail = db.NguoiDungs.Any(x =>
                    x.Email == email &&
                    x.MaNguoiDung != id
                );

                if (trungEmail)
                {
                    TempData["Error"] = "Email này đã được sử dụng!";
                    return RedirectToAction("Sua", new { id = id });
                }
            }

            // Cập nhật thông tin vào CSDL
            khachHang.HoTen = hoTen.Trim();
            khachHang.SoDienThoai = soDienThoai;
            khachHang.Email = email;

            if (!string.IsNullOrEmpty(trangThai))
            {
                khachHang.TrangThai = trangThai;
            }

            db.SaveChanges();

            TempData["Success"] = "Cập nhật thông tin khách hàng thành công!";

            return RedirectToAction("Index");
        }

        // =====================================================
        // KHÓA / MỞ KHÓA KHÁCH HÀNG
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DoiTrangThai(int id)
        {
            if (!KiemTraQuyen())
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            var khachHang = db.NguoiDungs
                .FirstOrDefault(x =>
                    x.MaNguoiDung == id &&
                    x.VaiTro == "KhachHang"
                );

            if (khachHang == null)
            {
                TempData["Error"] = "Không tìm thấy khách hàng!";
                return RedirectToAction("Index");
            }

            if (khachHang.TrangThai == "Hoạt động")
            {
                khachHang.TrangThai = "Đã khóa";
                TempData["Success"] = "Đã khóa tài khoản khách hàng!";
            }
            else
            {
                khachHang.TrangThai = "Hoạt động";
                TempData["Success"] = "Đã mở khóa tài khoản khách hàng!";
            }

            db.SaveChanges();

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