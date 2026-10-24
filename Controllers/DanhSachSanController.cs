using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Phân_Tích_Thiết_Kế_Website_QL_Sân_Bóng.Models;

namespace Phân_Tích_Thiết_Kế_Website_QL_Sân_Bóng.Controllers
{
    public class DanhSachSanController : Controller
    {
        private QLSanBongEntities db =
            new QLSanBongEntities();


        // =====================================================
        // THÔNG BÁO CHO NHÂN VIÊN KHI ADMIN THAY ĐỔI SÂN
        // =====================================================
        private void TaoThongBao(
            int maNguoiDung,
            string tieuDe,
            string noiDung,
            string duongDan = null)
        {
            var thongBao = new ThongBao
            {
                MaNguoiDung = maNguoiDung,
                TieuDe = tieuDe,
                NoiDung = noiDung,
                DuongDan = duongDan,
                DaDoc = false,
                NgayTao = DateTime.Now
            };

            db.ThongBaos.Add(thongBao);
        }


        private void GuiThongBaoChoNhanVien(
            string tieuDe,
            string noiDung)
        {
            var danhSachNhanVien =
                db.NguoiDungs
                .Where(n => n.VaiTro == "NhanVien")
                .ToList();

            string duongDan =
                Url.Action(
                    "DanhSachSan",
                    "DanhSachSan");

            foreach (var nhanVien in danhSachNhanVien)
            {
                TaoThongBao(
                    nhanVien.MaNguoiDung,
                    tieuDe,
                    noiDung,
                    duongDan);
            }
        }


        // =====================================================
        // DANH SÁCH / QUẢN LÝ SÂN
        // Admin      : quản lý
        // Nhân viên  : tra cứu
        // Khách hàng : xem và chọn sân đặt lịch
        // =====================================================
        [RoleAuthorize(Roles = "Admin,NhanVien,KhachHang")]
        [HttpGet]
        public ActionResult DanhSachSan()
        {
            var danhSach =
                db.SanBongs
                .OrderBy(s => s.MaSan)
                .ToList();

            return View(danhSach);
        }


        // =====================================================
        // ADMIN - CẬP NHẬT THÔNG TIN SÂN
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize(Roles = "Admin")]
        public ActionResult CapNhatSan(
            int maSan,
            string tenSan,
            string loaiSan,
            decimal donGiaTheoGio,
            string trangThai)
        {
            var san =
                db.SanBongs
                .FirstOrDefault(s =>
                    s.MaSan == maSan);

            if (san == null)
            {
                TempData["Loi"] =
                    "Không tìm thấy sân bóng cần cập nhật.";

                return RedirectToAction("DanhSachSan");
            }

            tenSan =
                (tenSan ?? "").Trim();

            loaiSan =
                (loaiSan ?? "").Trim();

            trangThai =
                (trangThai ?? "").Trim();


            if (string.IsNullOrWhiteSpace(tenSan))
            {
                TempData["Loi"] =
                    "Tên sân không được để trống.";

                return RedirectToAction("DanhSachSan");
            }


            if (string.IsNullOrWhiteSpace(loaiSan))
            {
                TempData["Loi"] =
                    "Vui lòng chọn loại sân.";

                return RedirectToAction("DanhSachSan");
            }


            if (donGiaTheoGio <= 0)
            {
                TempData["Loi"] =
                    "Đơn giá theo giờ phải lớn hơn 0.";

                return RedirectToAction("DanhSachSan");
            }


            string[] trangThaiHopLe =
            {
                "Hoạt động",
                "Bảo trì",
                "Ngừng hoạt động"
            };

            if (!trangThaiHopLe.Contains(trangThai))
            {
                TempData["Loi"] =
                    "Trạng thái sân không hợp lệ.";

                return RedirectToAction("DanhSachSan");
            }


            bool trungTen =
                db.SanBongs.Any(s =>
                    s.MaSan != maSan &&
                    s.TenSan == tenSan);

            if (trungTen)
            {
                TempData["Loi"] =
                    "Tên sân đã tồn tại.";

                return RedirectToAction("DanhSachSan");
            }


            // Ghi lại các thay đổi để gửi thông báo cho Nhân viên.
            var noiDungThayDoi =
                new List<string>();

            string tenSanCu =
                san.TenSan;

            string loaiSanCu =
                san.LoaiSan;

            decimal? donGiaCu =
                san.DonGiaTheoGio;

            string trangThaiCu =
                san.TrangThai;


            if (tenSanCu != tenSan)
            {
                noiDungThayDoi.Add(
                    "Tên sân: " +
                    tenSanCu +
                    " → " +
                    tenSan);
            }

            if (loaiSanCu != loaiSan)
            {
                noiDungThayDoi.Add(
                    "Loại sân: " +
                    loaiSanCu +
                    " → " +
                    loaiSan);
            }

            if ((donGiaCu ?? 0m) != donGiaTheoGio)
            {
                noiDungThayDoi.Add(
                    "Đơn giá: " +
                    (donGiaCu ?? 0m).ToString("N0") +
                    " VNĐ/giờ → " +
                    donGiaTheoGio.ToString("N0") +
                    " VNĐ/giờ");
            }

            if (trangThaiCu != trangThai)
            {
                noiDungThayDoi.Add(
                    "Trạng thái: " +
                    trangThaiCu +
                    " → " +
                    trangThai);
            }


            san.TenSan =
                tenSan;

            san.LoaiSan =
                loaiSan;

            san.DonGiaTheoGio =
                donGiaTheoGio;

            san.TrangThai =
                trangThai;


            // Chỉ gửi thông báo khi thực sự có thay đổi.
            if (noiDungThayDoi.Any())
            {
                string noiDungThongBao =
                    "Quản trị viên vừa cập nhật " +
                    san.TenSan +
                    ". " +
                    string.Join(
                        "; ",
                        noiDungThayDoi) +
                    ". Vui lòng nắm thông tin để hỗ trợ khách hàng khi cần.";

                GuiThongBaoChoNhanVien(
                    "Thông tin sân bóng vừa thay đổi",
                    noiDungThongBao);
            }


            db.SaveChanges();


            TempData["ThanhCong"] =
                noiDungThayDoi.Any()
                    ? "Đã cập nhật thông tin " +
                      san.TenSan +
                      " và thông báo cho Nhân viên."
                    : "Không có thông tin nào thay đổi.";

            return RedirectToAction("DanhSachSan");
        }


        // =====================================================
        // ADMIN - ĐỔI NHANH TRẠNG THÁI SÂN
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize(Roles = "Admin")]
        public ActionResult DoiTrangThaiSan(
            int maSan,
            string trangThaiMoi)
        {
            var san =
                db.SanBongs
                .FirstOrDefault(s =>
                    s.MaSan == maSan);

            if (san == null)
            {
                TempData["Loi"] =
                    "Không tìm thấy sân bóng.";

                return RedirectToAction("DanhSachSan");
            }


            trangThaiMoi =
                (trangThaiMoi ?? "").Trim();


            string[] trangThaiHopLe =
            {
                "Hoạt động",
                "Bảo trì",
                "Ngừng hoạt động"
            };

            if (!trangThaiHopLe.Contains(trangThaiMoi))
            {
                TempData["Loi"] =
                    "Trạng thái sân không hợp lệ.";

                return RedirectToAction("DanhSachSan");
            }


            string trangThaiCu =
                san.TrangThai;

            san.TrangThai =
                trangThaiMoi;


            if (trangThaiCu != trangThaiMoi)
            {
                string noiDungThongBao =
                    "Quản trị viên vừa thay đổi trạng thái " +
                    san.TenSan +
                    ": " +
                    trangThaiCu +
                    " → " +
                    trangThaiMoi +
                    ". " +
                    (trangThaiMoi == "Hoạt động"
                        ? "Sân đã có thể nhận đơn đặt sân mới."
                        : "Sân tạm thời không nhận đơn đặt sân mới.") +
                    " Vui lòng nắm thông tin để hỗ trợ khách hàng khi cần.";

                GuiThongBaoChoNhanVien(
                    "Trạng thái sân bóng vừa thay đổi",
                    noiDungThongBao);
            }


            db.SaveChanges();


            if (trangThaiCu == trangThaiMoi)
            {
                TempData["ThanhCong"] =
                    "Trạng thái sân không thay đổi.";
            }
            else if (trangThaiMoi == "Hoạt động")
            {
                TempData["ThanhCong"] =
                    san.TenSan +
                    " đã được mở lại và đã thông báo cho Nhân viên.";
            }
            else
            {
                TempData["ThanhCong"] =
                    san.TenSan +
                    " đã chuyển sang trạng thái " +
                    trangThaiMoi +
                    " và đã thông báo cho Nhân viên.";
            }


            return RedirectToAction("DanhSachSan");
        }


        protected override void Dispose(
            bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
