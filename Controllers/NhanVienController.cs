using System;
using System.Linq;
using System.Web.Mvc;
using Phân_Tích_Thiết_Kế_Website_QL_Sân_Bóng.Models;

namespace Phân_Tích_Thiết_Kế_Website_QL_Sân_Bóng.Controllers
{
    public class NhanVienController : Controller
    {
        private QLSanBongEntities db = new QLSanBongEntities();

        // GET: NhanVien
        public ActionResult Index(string tuKhoa)
        {
            // Chỉ cho phép Admin truy cập
            var vaiTro = Session["VaiTro"] as string;
            if (Session["MaNguoiDung"] == null || vaiTro != "Admin")
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            // Lọc danh sách tài khoản thuộc vai trò Nhân viên (MaVaiTro = 2 hoặc VaiTro = "NhanVien")
            var dsNhanVien = db.NguoiDungs.Where(n => n.VaiTro == "NhanVien").AsQueryable();

            if (!string.IsNullOrEmpty(tuKhoa))
            {
                dsNhanVien = dsNhanVien.Where(n => n.HoTen.Contains(tuKhoa) || n.SoDienThoai.Contains(tuKhoa));
            }

            ViewBag.TuKhoa = tuKhoa;
            return View(dsNhanVien.OrderByDescending(n => n.MaNguoiDung).ToList());
        }

        // POST: NhanVien/ThemMoi
        [HttpPost]
        public ActionResult ThemMoi(string hoTen, string soDienThoai, string email, string matKhau)
        {
            if (Session["VaiTro"]?.ToString() != "Admin")
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            // Kiểm tra số điện thoại trùng
            var tonTai = db.NguoiDungs.FirstOrDefault(n => n.SoDienThoai == soDienThoai);
            if (tonTai != null)
            {
                TempData["Error"] = "Số điện thoại này đã được đăng ký!";
                return RedirectToAction("Index");
            }

            var nv = new NguoiDung
            {
                HoTen = hoTen,
                SoDienThoai = soDienThoai,
                Email = email,
                MatKhau = matKhau,
                VaiTro = "NhanVien", // Chỉ dùng thuộc tính này
                TrangThai = "Hoạt động",
                NgayTao = DateTime.Now
            };

            db.NguoiDungs.Add(nv);
            db.SaveChanges();

            TempData["Success"] = "Thêm tài khoản nhân viên mới thành công!";
            return RedirectToAction("Index");
        }

        // POST: NhanVien/DoiTrangThai
        [HttpPost]
        public ActionResult DoiTrangThai(int id)
        {
            if (Session["VaiTro"]?.ToString() != "Admin")
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            var nv = db.NguoiDungs.Find(id);
            if (nv != null)
            {
                if (nv.TrangThai == "Hoạt động")
                {
                    nv.TrangThai = "Đã khóa";
                    TempData["Success"] = "Đã khóa tài khoản nhân viên " + nv.HoTen;
                }
                else
                {
                    nv.TrangThai = "Hoạt động";
                    TempData["Success"] = "Đã mở khóa tài khoản nhân viên " + nv.HoTen;
                }
                db.SaveChanges();
            }
            return RedirectToAction("Index");
        }
    }
}