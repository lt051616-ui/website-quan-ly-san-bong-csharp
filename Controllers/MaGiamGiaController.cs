using System;
using System.Linq;
using System.Web.Mvc;
using Phân_Tích_Thiết_Kế_Website_QL_Sân_Bóng.Models;

namespace Phân_Tích_Thiết_Kế_Website_QL_Sân_Bóng.Controllers
{
    public class MaGiamGiaController : Controller
    {
        private QLSanBongEntities db = new QLSanBongEntities();

        private bool DaDangNhap()
        {
            return Session["MaNguoiDung"] != null;
        }

        private string LayVaiTro()
        {
            return Session["VaiTro"] != null
                ? Session["VaiTro"].ToString()
                : "";
        }

        private bool LaAdmin()
        {
            return DaDangNhap() &&
                   LayVaiTro() == "Admin";
        }

        private ActionResult ChuyenHuongNeuChuaDangNhap()
        {
            return RedirectToAction("DangNhap", "TaiKhoan");
        }

        private string KiemTraDuLieu(MaGiamGia model, bool dangSua = false)
        {
            if (model == null)
                return "Dữ liệu mã giảm giá không hợp lệ.";

            if (string.IsNullOrWhiteSpace(model.MaCode))
                return "Vui lòng nhập mã giảm giá.";

            string maCode = model.MaCode.Trim().ToUpper();

            bool biTrung = dangSua
                ? db.MaGiamGias.Any(x =>
                    x.MaCode.ToUpper() == maCode &&
                    x.MaGiamGiaID != model.MaGiamGiaID)
                : db.MaGiamGias.Any(x =>
                    x.MaCode.ToUpper() == maCode);

            if (biTrung)
                return "Mã giảm giá đã tồn tại.";

            if (model.NgayKetThuc.Date < model.NgayBatDau.Date)
                return "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.";

            if (model.SoLuong < 0)
                return "Số lượng không được nhỏ hơn 0.";

            if (model.GiaTriDonHangToiTheu.HasValue &&
                model.GiaTriDonHangToiTheu.Value < 0)
                return "Giá trị đơn hàng tối thiểu không hợp lệ.";

            if (model.SoTienGiamToiDa.HasValue &&
                model.SoTienGiamToiDa.Value < 0)
                return "Số tiền giảm tối đa không hợp lệ.";

            bool coPhanTram =
                model.PhanTramGiam.HasValue &&
                model.PhanTramGiam.Value > 0;

            bool coSoTien =
                model.SoTienGiam.HasValue &&
                model.SoTienGiam.Value > 0;

            if (!coPhanTram && !coSoTien)
                return "Vui lòng nhập phần trăm giảm hoặc số tiền giảm.";

            if (coPhanTram && coSoTien)
                return "Chỉ được chọn một hình thức giảm: theo phần trăm hoặc theo số tiền.";

            if (coPhanTram && model.PhanTramGiam.Value > 100)
                return "Phần trăm giảm không được lớn hơn 100%.";

            return null;
        }

        [HttpGet]
        public ActionResult Index()
        {
            if (!DaDangNhap())
                return ChuyenHuongNeuChuaDangNhap();

            var ds = db.MaGiamGias
                .OrderByDescending(x => x.NgayBatDau)
                .ThenByDescending(x => x.MaGiamGiaID)
                .ToList();

            return View(ds);
        }

        [HttpGet]
        public ActionResult Them()
        {
            if (!DaDangNhap())
                return ChuyenHuongNeuChuaDangNhap();

            if (!LaAdmin())
                return new HttpStatusCodeResult(403);

            var model = new MaGiamGia
            {
                NgayBatDau = DateTime.Today,
                NgayKetThuc = DateTime.Today.AddDays(30),
                SoLuong = 1,
                TrangThai = 1
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Them(MaGiamGia model)
        {
            if (!DaDangNhap())
                return ChuyenHuongNeuChuaDangNhap();

            if (!LaAdmin())
                return new HttpStatusCodeResult(403);

            string loi = KiemTraDuLieu(model);

            if (!string.IsNullOrEmpty(loi))
            {
                ModelState.AddModelError("", loi);
                return View(model);
            }

            try
            {
                model.MaCode = model.MaCode.Trim().ToUpper();

                if (model.PhanTramGiam.HasValue &&
                    model.PhanTramGiam.Value > 0)
                {
                    model.SoTienGiam = null;
                }
                else
                {
                    model.PhanTramGiam = null;
                    model.SoTienGiamToiDa = null;
                }

                model.TrangThai = 1;

                if (model.SoLuong <= 0 ||
                    model.NgayKetThuc.Date < DateTime.Today)
                {
                    model.TrangThai = 0;
                }

                db.MaGiamGias.Add(model);
                db.SaveChanges();

                TempData["SuccessMessage"] =
                    "Thêm mã giảm giá thành công!";

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(
                    "",
                    "Không thể thêm mã giảm giá. " + ex.Message);

                return View(model);
            }
        }

        [HttpGet]
        public ActionResult Sua(int id)
        {
            if (!DaDangNhap())
                return ChuyenHuongNeuChuaDangNhap();

            if (!LaAdmin())
                return new HttpStatusCodeResult(403);

            var ma = db.MaGiamGias.Find(id);

            if (ma == null)
                return HttpNotFound();

            return View(ma);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Sua(MaGiamGia model)
        {
            if (!DaDangNhap())
                return ChuyenHuongNeuChuaDangNhap();

            if (!LaAdmin())
                return new HttpStatusCodeResult(403);

            string loi = KiemTraDuLieu(model, true);

            if (!string.IsNullOrEmpty(loi))
            {
                ModelState.AddModelError("", loi);
                return View(model);
            }

            var ma = db.MaGiamGias.Find(model.MaGiamGiaID);

            if (ma == null)
                return HttpNotFound();

            try
            {
                ma.MaCode = model.MaCode.Trim().ToUpper();
                ma.PhanTramGiam = model.PhanTramGiam;
                ma.SoTienGiam = model.SoTienGiam;
                ma.GiaTriDonHangToiTheu = model.GiaTriDonHangToiTheu;
                ma.SoTienGiamToiDa = model.SoTienGiamToiDa;
                ma.SoLuong = model.SoLuong;
                ma.NgayBatDau = model.NgayBatDau;
                ma.NgayKetThuc = model.NgayKetThuc;

                if (ma.PhanTramGiam.HasValue &&
                    ma.PhanTramGiam.Value > 0)
                {
                    ma.SoTienGiam = null;
                }
                else
                {
                    ma.PhanTramGiam = null;
                    ma.SoTienGiamToiDa = null;
                }

                ma.TrangThai = model.TrangThai ?? 1;

                if (ma.SoLuong <= 0 ||
                    ma.NgayKetThuc.Date < DateTime.Today)
                {
                    ma.TrangThai = 0;
                }

                db.SaveChanges();

                TempData["SuccessMessage"] =
                    "Cập nhật mã giảm giá thành công!";

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(
                    "",
                    "Không thể cập nhật mã giảm giá. " + ex.Message);

                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Xoa(int id)
        {
            if (!DaDangNhap())
                return ChuyenHuongNeuChuaDangNhap();

            if (!LaAdmin())
                return new HttpStatusCodeResult(403);

            var ma = db.MaGiamGias.Find(id);

            if (ma == null)
                return HttpNotFound();

            try
            {
                // Không xóa vật lý để tránh mất lịch sử đơn.
                ma.TrangThai = 0;
                db.SaveChanges();

                TempData["SuccessMessage"] =
                    "Đã ngừng mã giảm giá " +
                    ma.MaCode + "!";

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] =
                    "Không thể ngừng mã giảm giá. " +
                    ex.Message;

                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BatLai(int id)
        {
            if (!DaDangNhap())
                return ChuyenHuongNeuChuaDangNhap();

            if (!LaAdmin())
                return new HttpStatusCodeResult(403);

            var ma = db.MaGiamGias.Find(id);

            if (ma == null)
                return HttpNotFound();

            if (ma.SoLuong <= 0)
            {
                TempData["ErrorMessage"] =
                    "Mã giảm giá đã hết lượt sử dụng.";

                return RedirectToAction("Index");
            }

            if (ma.NgayKetThuc.Date < DateTime.Today)
            {
                TempData["ErrorMessage"] =
                    "Mã giảm giá đã hết hạn. Hãy sửa ngày kết thúc trước khi bật lại.";

                return RedirectToAction("Index");
            }

            ma.TrangThai = 1;
            db.SaveChanges();

            TempData["SuccessMessage"] =
                "Đã bật lại mã giảm giá " +
                ma.MaCode + "!";

            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();

            base.Dispose(disposing);
        }
    }
}
