using System;
using System.Linq;
using System.Data.Entity;
using System.Web.Mvc;
using Phân_Tích_Thiết_Kế_Website_QL_Sân_Bóng.Models;

namespace Phân_Tích_Thiết_Kế_Website_QL_Sân_Bóng.Controllers
{
    public class DichVuController : Controller
    {
        private QLSanBongEntities db = new QLSanBongEntities();


        // =====================================================
        // DANH SÁCH DỊCH VỤ
        // Admin / Nhân viên / Khách hàng đều được xem
        // URL: /DichVu/Index
        // =====================================================
        [RoleAuthorize(Roles = "Admin,NhanVien,KhachHang")]
        public ActionResult Index(string tuKhoa)
        {
            var dsDichVu = db.DichVus.AsQueryable();

            // ================================================
            // TÌM KIẾM
            // ================================================
            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                tuKhoa = tuKhoa.Trim();

                dsDichVu = dsDichVu.Where(d =>
                    d.TenDichVu.Contains(tuKhoa));
            }

            ViewBag.TuKhoa = tuKhoa;


            // ================================================
            // KIỂM TRA VAI TRÒ
            // ================================================
            string vaiTro = Session["VaiTro"] as string;

            bool laAdmin = vaiTro == "Admin";
            bool laNhanVien = vaiTro == "NhanVien";
            bool laKhachHang = vaiTro == "KhachHang";

            ViewBag.CoQuyenQuanLy =
                laAdmin || laNhanVien;


            // ================================================
            // DANH SÁCH ĐƠN CÓ THỂ THÊM DỊCH VỤ
            // ================================================
            // Cách đơn giản:
            // - Nhân viên: chọn đơn của khách đã thanh toán.
            // - Khách hàng: chỉ thấy đơn của chính mình.
            // - Đơn đã hủy / hoàn thành / đang yêu cầu hủy không được thêm.
            var dsDatSan = db.DatSans
                .Include(d => d.SanBong)
                .Include(d => d.KhungGio)
                .Include(d => d.NguoiDung)
                .Where(d =>
                    d.TrangThai != "Đã hủy" &&
                    d.TrangThai != "Hoàn thành" &&
                    d.TrangThai != "Đã hoàn thành" &&
                    d.TrangThai != "Yêu cầu hủy");

            if (laNhanVien)
            {
                // Nhân viên bán thêm dịch vụ cho các đơn đã thanh toán.
                dsDatSan = dsDatSan.Where(d =>
                    d.TrangThaiThanhToan == "Đã thanh toán");
            }
            else if (laKhachHang)
            {
                int maNguoiDung;

                if (Session["MaNguoiDung"] == null ||
                    !int.TryParse(
                        Session["MaNguoiDung"].ToString(),
                        out maNguoiDung))
                {
                    ViewBag.DsDatSan =
                        new System.Collections.Generic.List<DatSan>();
                }
                else
                {
                    dsDatSan = dsDatSan.Where(d =>
                        d.MaNguoiDung == maNguoiDung);

                    ViewBag.DsDatSan = dsDatSan
                        .OrderByDescending(d => d.MaDatSan)
                        .ToList();
                }
            }
            else if (laAdmin)
            {
                // Admin giữ quyền xem dữ liệu quản lý,
                // nhưng giao diện Admin không dùng nút Đặt Dịch Vụ.
                ViewBag.DsDatSan = dsDatSan
                    .OrderByDescending(d => d.MaDatSan)
                    .ToList();
            }
            else
            {
                ViewBag.DsDatSan =
                    new System.Collections.Generic.List<DatSan>();
            }

            if (laNhanVien)
            {
                ViewBag.DsDatSan = dsDatSan
                    .OrderByDescending(d => d.MaDatSan)
                    .ToList();
            }


            // ================================================
            // TRẢ DANH SÁCH DỊCH VỤ
            // ================================================
            var ketQua = dsDichVu
                .OrderByDescending(d => d.MaDichVu)
                .ToList();

            return View(ketQua);
        }


        // =====================================================
        // THÊM DỊCH VỤ
        // Chỉ Admin / Nhân viên
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize(Roles = "Admin,NhanVien")]
        public ActionResult ThemMoi(
            string tenDichVu,
            decimal donGia,
            int soLuongTon)
        {
            if (string.IsNullOrWhiteSpace(tenDichVu))
            {
                TempData["Error"] =
                    "Tên dịch vụ không được để trống!";

                return RedirectToAction("Index");
            }

            if (donGia < 0)
            {
                TempData["Error"] =
                    "Đơn giá không được nhỏ hơn 0!";

                return RedirectToAction("Index");
            }

            if (soLuongTon < 0)
            {
                soLuongTon = 0;
            }

            var dv = new DichVu
            {
                TenDichVu = tenDichVu.Trim(),
                DonGia = donGia,
                SoLuongTon = soLuongTon
            };

            db.DichVus.Add(dv);
            db.SaveChanges();

            TempData["Success"] =
                "Thêm dịch vụ mới thành công!";

            return RedirectToAction("Index");
        }


        // =====================================================
        // GET: SỬA DỊCH VỤ
        // Chỉ Admin / Nhân viên
        // =====================================================
        [HttpGet]
        [RoleAuthorize(Roles = "Admin,NhanVien")]
        public ActionResult Sua(int id)
        {
            var dv = db.DichVus.Find(id);

            if (dv == null)
            {
                TempData["Error"] =
                    "Không tìm thấy dịch vụ cần sửa!";

                return RedirectToAction("Index");
            }

            return View(dv);
        }


        // =====================================================
        // POST: SỬA DỊCH VỤ
        // Chỉ Admin / Nhân viên
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize(Roles = "Admin,NhanVien")]
        public ActionResult Sua(
            int id,
            string tenDichVu,
            decimal donGia)
        {
            var dv = db.DichVus.Find(id);

            if (dv == null)
            {
                TempData["Error"] =
                    "Không tìm thấy dịch vụ!";

                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(tenDichVu))
            {
                TempData["Error"] =
                    "Tên dịch vụ không được để trống!";

                return RedirectToAction(
                    "Sua",
                    new { id = id }
                );
            }

            if (donGia < 0)
            {
                TempData["Error"] =
                    "Đơn giá không được nhỏ hơn 0!";

                return RedirectToAction(
                    "Sua",
                    new { id = id }
                );
            }

            dv.TenDichVu = tenDichVu.Trim();
            dv.DonGia = donGia;

            db.SaveChanges();

            TempData["Success"] =
                "Cập nhật thông tin dịch vụ thành công!";

            return RedirectToAction("Index");
        }


        // =====================================================
        // NHẬP THÊM KHO
        // Chỉ Admin / Nhân viên
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize(Roles = "Admin,NhanVien")]
        public ActionResult CapNhatKho(
            int maDichVu,
            int soLuongThem)
        {
            var dv = db.DichVus.Find(maDichVu);

            if (dv == null)
            {
                TempData["Error"] =
                    "Không tìm thấy dịch vụ trong kho!";

                return RedirectToAction("Index");
            }

            if (soLuongThem <= 0)
            {
                TempData["Error"] =
                    "Số lượng nhập thêm phải lớn hơn 0!";

                return RedirectToAction("Index");
            }

            dv.SoLuongTon += soLuongThem;

            db.SaveChanges();

            TempData["Success"] =
                $"Đã nhập thêm {soLuongThem} sản phẩm vào kho!";

            return RedirectToAction("Index");
        }


        // =====================================================
        // ĐẶT THÊM DỊCH VỤ CHO ĐƠN ĐẶT SÂN
        // Cách đơn giản:
        // - Thêm/cộng dịch vụ vào ChiTietDichVuDat
        // - Trừ tồn kho
        // - Cộng tiền dịch vụ vào TongTien của đơn
        // - KHÔNG đổi lại trạng thái thanh toán của tiền sân
        // - Nhân viên thu ngay tiền dịch vụ phát sinh bằng
        //   Tiền mặt hoặc Chuyển khoản
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize(Roles = "NhanVien,KhachHang")]
        public ActionResult DatDichVu(
            int maDatSan,
            int maDichVu,
            int soLuong,
            string hinhThucThanhToanDichVu)
        {
            string vaiTro =
                Session["VaiTro"] as string ?? "";

            var dv = db.DichVus
                .FirstOrDefault(d =>
                    d.MaDichVu == maDichVu);

            var datSan = db.DatSans
                .Include(d => d.KhungGio)
                .Include(d => d.NguoiDung)
                .FirstOrDefault(d =>
                    d.MaDatSan == maDatSan);

            if (dv == null || datSan == null)
            {
                TempData["Error"] =
                    "Thông tin dịch vụ hoặc đơn đặt sân không hợp lệ.";

                return RedirectToAction("Index");
            }

            // Khách hàng chỉ được thêm dịch vụ vào đơn của chính mình.
            if (vaiTro == "KhachHang")
            {
                int maNguoiDung;

                if (Session["MaNguoiDung"] == null ||
                    !int.TryParse(
                        Session["MaNguoiDung"].ToString(),
                        out maNguoiDung) ||
                    datSan.MaNguoiDung != maNguoiDung)
                {
                    return new HttpStatusCodeResult(403);
                }
            }

            // Không thêm dịch vụ vào đơn không còn hoạt động.
            if (datSan.TrangThai == "Đã hủy" ||
                datSan.TrangThai == "Yêu cầu hủy" ||
                datSan.TrangThai == "Hoàn thành" ||
                datSan.TrangThai == "Đã hoàn thành")
            {
                TempData["Error"] =
                    "Đơn đặt sân này không còn được phép thêm dịch vụ.";

                return RedirectToAction("Index");
            }

            // Với Nhân viên, chỉ bán thêm cho đơn đã thanh toán.
            if (vaiTro == "NhanVien" &&
                datSan.TrangThaiThanhToan != "Đã thanh toán")
            {
                TempData["Error"] =
                    "Nhân viên chỉ thêm dịch vụ phát sinh cho đơn đã thanh toán.";

                return RedirectToAction("Index");
            }

            if (soLuong <= 0)
            {
                TempData["Error"] =
                    "Số lượng dịch vụ phải lớn hơn 0.";

                return RedirectToAction("Index");
            }

            if (dv.SoLuongTon < soLuong)
            {
                TempData["Error"] =
                    "Số lượng tồn kho không đủ. Hiện chỉ còn " +
                    dv.SoLuongTon + ".";

                return RedirectToAction("Index");
            }

            // Nhân viên phải thu tiền phần dịch vụ phát sinh ngay.
            if (vaiTro == "NhanVien")
            {
                hinhThucThanhToanDichVu =
                    (hinhThucThanhToanDichVu ?? "").Trim();

                if (hinhThucThanhToanDichVu != "Tiền mặt" &&
                    hinhThucThanhToanDichVu != "Chuyển khoản")
                {
                    TempData["Error"] =
                        "Vui lòng chọn thanh toán dịch vụ bằng Tiền mặt hoặc Chuyển khoản.";

                    return RedirectToAction("Index");
                }
            }

            using (var transaction =
                db.Database.BeginTransaction())
            {
                try
                {
                    var chiTiet =
                        db.ChiTietDichVuDats
                        .FirstOrDefault(c =>
                            c.MaDatSan == maDatSan &&
                            c.MaDichVu == maDichVu);

                    // Nếu dịch vụ đã có trong đơn thì giữ đơn giá
                    // của lần ghi nhận trước đó; nếu chưa có thì
                    // dùng giá hiện tại.
                    decimal donGiaApDung =
                        chiTiet != null
                            ? chiTiet.DonGiaTaiThoiDiemThat
                            : dv.DonGia;

                    decimal tienPhatSinh =
                        donGiaApDung * soLuong;

                    if (chiTiet != null)
                    {
                        chiTiet.SoLuong += soLuong;
                    }
                    else
                    {
                        db.ChiTietDichVuDats.Add(
                            new ChiTietDichVuDat
                            {
                                MaDatSan = maDatSan,
                                MaDichVu = maDichVu,
                                SoLuong = soLuong,
                                DonGiaTaiThoiDiemThat =
                                    donGiaApDung
                            });
                    }

                    // Trừ kho.
                    dv.SoLuongTon -= soLuong;

                    // Chỉ cộng tiền dịch vụ mới vào tổng đơn.
                    // Không đổi TrangThaiThanhToan của đơn:
                    // đơn đã thanh toán vẫn là "Đã thanh toán".
                    datSan.TongTien =
                        (datSan.TongTien ?? 0m) +
                        tienPhatSinh;

                    db.SaveChanges();
                    transaction.Commit();

                    string noiDungThanhToan =
                        vaiTro == "NhanVien"
                            ? " Đã thu " +
                              tienPhatSinh.ToString("N0") +
                              " VNĐ bằng " +
                              hinhThucThanhToanDichVu + "."
                            : "";

                    TempData["Success"] =
                        "Đã thêm " +
                        soLuong + " " +
                        dv.TenDichVu +
                        " vào đơn #" +
                        maDatSan + "." +
                        noiDungThanhToan;

                    return RedirectToAction("Index");
                }
                catch
                {
                    try
                    {
                        transaction.Rollback();
                    }
                    catch
                    {
                    }

                    TempData["Error"] =
                        "Không thể thêm dịch vụ. Vui lòng thử lại.";

                    return RedirectToAction("Index");
                }
            }
        }


        // =====================================================
        // BÁN DỊCH VỤ CHO ĐƠN ĐẶT SÂN
        // Chỉ Admin / Nhân viên
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize(Roles = "Admin,NhanVien")]
        public ActionResult BanDichVu(
            int maDatSan,
            int maDichVu,
            int soLuong)
        {
            var dv = db.DichVus.Find(maDichVu);

            var datSan = db.DatSans.Find(maDatSan);

            if (dv == null || datSan == null)
            {
                TempData["Error"] =
                    "Thông tin dịch vụ hoặc phiếu đặt sân không hợp lệ!";

                return RedirectToAction("Index");
            }


            // ================================================
            // KIỂM TRA SỐ LƯỢNG
            // ================================================
            if (soLuong <= 0)
            {
                TempData["Error"] =
                    "Số lượng bán phải lớn hơn 0!";

                return RedirectToAction("Index");
            }


            // ================================================
            // KIỂM TRA TỒN KHO
            // ================================================
            if (dv.SoLuongTon < soLuong)
            {
                TempData["Error"] =
                    $"Số lượng tồn kho không đủ! " +
                    $"(Chỉ còn {dv.SoLuongTon} sản phẩm)";

                return RedirectToAction("Index");
            }


            // ================================================
            // TRỪ TỒN KHO
            // ================================================
            dv.SoLuongTon -= soLuong;


            // ================================================
            // KIỂM TRA DỊCH VỤ ĐÃ CÓ TRONG ĐƠN CHƯA
            // ================================================
            var chiTiet =
                db.ChiTietDichVuDats.FirstOrDefault(c =>
                    c.MaDatSan == maDatSan &&
                    c.MaDichVu == maDichVu);


            if (chiTiet != null)
            {
                // Đã có thì cộng thêm số lượng
                chiTiet.SoLuong += soLuong;
            }
            else
            {
                // Chưa có thì tạo mới
                var ctMoi = new ChiTietDichVuDat
                {
                    MaDatSan = maDatSan,
                    MaDichVu = maDichVu,
                    SoLuong = soLuong,

                    DonGiaTaiThoiDiemThat =
                        dv.DonGia
                };

                db.ChiTietDichVuDats.Add(ctMoi);
            }


            // ================================================
            // CẬP NHẬT TỔNG TIỀN ĐẶT SÂN
            // ================================================
            datSan.TongTien =
                (datSan.TongTien ?? 0) +
                (dv.DonGia * soLuong);


            db.SaveChanges();


            TempData["Success"] =
                $"Đã bán thành công {soLuong} " +
                $"{dv.TenDichVu} cho đơn đặt sân #{maDatSan}!";


            return RedirectToAction("Index");
        }


        // =====================================================
        // XÓA DỊCH VỤ
        // Chỉ Admin / Nhân viên
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize(Roles = "Admin,NhanVien")]
        public ActionResult Xoa(int id)
        {
            var dv = db.DichVus.Find(id);

            if (dv == null)
            {
                TempData["Error"] =
                    "Không tìm thấy dịch vụ!";

                return RedirectToAction("Index");
            }


            // ================================================
            // KIỂM TRA DỊCH VỤ ĐÃ CÓ GIAO DỊCH CHƯA
            // ================================================
            bool coGiaoDich =
                db.ChiTietDichVuDats.Any(c =>
                    c.MaDichVu == id);


            if (coGiaoDich)
            {
                TempData["Error"] =
                    "Không thể xóa dịch vụ này vì đã có " +
                    "lịch sử khách hàng mua/sử dụng trong " +
                    "các đơn đặt sân!";

                return RedirectToAction("Index");
            }


            db.DichVus.Remove(dv);

            db.SaveChanges();


            TempData["Success"] =
                "Đã xóa dịch vụ thành công!";


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