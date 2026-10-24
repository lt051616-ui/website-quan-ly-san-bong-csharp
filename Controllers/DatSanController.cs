using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using Phân_Tích_Thiết_Kế_Website_QL_Sân_Bóng.Models;

namespace Phân_Tích_Thiết_Kế_Website_QL_Sân_Bóng.Controllers
{
    public class DatSanController : Controller
    {
        private QLSanBongEntities db = new QLSanBongEntities();

        // =========================================================
        // CHÍNH SÁCH THỜI GIAN ĐẶT SÂN
        // Khách được đặt từ hôm nay đến tối đa 7 ngày tới.
        // Nếu muốn mở lịch 14 ngày, chỉ cần đổi 7 thành 14.
        // =========================================================
        private const int SO_NGAY_DAT_TRUOC_TOI_DA = 7;

        // =========================================================
        // MODEL DỊCH VỤ
        // =========================================================
        public class DichVuChonModel
        {
            public int MaDichVu { get; set; }
            public int SoLuong { get; set; }
        }

        // =========================================================
        // MODEL SÂN ĐƯỢC CHỌN
        // =========================================================
        public class ItemSanChonModel
        {
            public int MaSan { get; set; }

            // Giao diện mới cho khách tự chọn giờ.
            // MaKhungGio vẫn được giữ để tương thích dữ liệu cũ;
            // Controller sẽ tự tìm/tạo KhungGio tương ứng trước khi lưu đơn.
            public int MaKhungGio { get; set; }
            public string GioBatDau { get; set; }
            public string GioKetThuc { get; set; }

            public List<DichVuChonModel> DichVus { get; set; }
        }

        // =========================================================
        // KIỂM TRA ĐĂNG NHẬP
        // =========================================================
        private bool DaDangNhap()
        {
            return Session["MaNguoiDung"] != null;
        }

        // =========================================================
        // LẤY MÃ NGƯỜI DÙNG
        // =========================================================
        private int? LayMaNguoiDung()
        {
            if (Session["MaNguoiDung"] == null)
                return null;

            int maNguoiDung;

            if (int.TryParse(
                Session["MaNguoiDung"].ToString(),
                out maNguoiDung))
            {
                return maNguoiDung;
            }

            return null;
        }

        // =========================================================
        // LẤY VAI TRÒ
        // =========================================================
        private string LayVaiTro()
        {
            if (Session["VaiTro"] == null)
                return "";

            return Session["VaiTro"].ToString();
        }

        // =========================================================
        // TẠO THÔNG BÁO
        // =========================================================
        private void TaoThongBao(
            int? maNguoiDung,
            string tieuDe,
            string noiDung,
            string duongDan = null)
        {
            // Khách vãng lai có thể không có tài khoản,
            // nên DatSan.MaNguoiDung có thể NULL.
            // Trường hợp này không tạo ThongBao trong tài khoản.
            if (!maNguoiDung.HasValue)
            {
                return;
            }

            var thongBao = new ThongBao
            {
                MaNguoiDung = maNguoiDung.Value,
                TieuDe = tieuDe,
                NoiDung = noiDung,
                DuongDan = duongDan,
                DaDoc = false,
                NgayTao = DateTime.Now
            };

            db.ThongBaos.Add(thongBao);
        }

        // =========================================================
        // KIỂM TRA QUYỀN XEM ĐƠN
        // =========================================================
        private bool CoQuyenXemDon(DatSan datSan)
        {
            int? maNguoiDung = LayMaNguoiDung();
            string vaiTro = LayVaiTro();

            if (!maNguoiDung.HasValue)
                return false;

            if (vaiTro == "Admin" || vaiTro == "NhanVien")
                return true;

            return datSan.MaNguoiDung == maNguoiDung.Value;
        }

        // =========================================================
        // TÍNH TIỀN GIẢM GIÁ
        // =========================================================
        private decimal TinhTienGiam(
            MaGiamGia maGiamGia,
            decimal tongTien)
        {
            if (maGiamGia == null || tongTien <= 0)
                return 0;

            decimal tienGiam = 0;

            // Giảm theo %
            if (maGiamGia.PhanTramGiam.HasValue &&
                maGiamGia.PhanTramGiam.Value > 0)
            {
                double phanTram = maGiamGia.PhanTramGiam.Value;

                tienGiam =
                    tongTien *
                    (decimal)(phanTram / 100.0);
            }

            // Giảm số tiền cố định
            if (maGiamGia.SoTienGiam.HasValue &&
                maGiamGia.SoTienGiam.Value > 0)
            {
                tienGiam += maGiamGia.SoTienGiam.Value;
            }

            // Giới hạn giảm tối đa
            if (maGiamGia.SoTienGiamToiDa.HasValue &&
                maGiamGia.SoTienGiamToiDa.Value > 0 &&
                tienGiam > maGiamGia.SoTienGiamToiDa.Value)
            {
                tienGiam = maGiamGia.SoTienGiamToiDa.Value;
            }

            // Không được giảm quá tổng tiền
            if (tienGiam > tongTien)
                tienGiam = tongTien;

            if (tienGiam < 0)
                tienGiam = 0;

            return Math.Round(
                tienGiam,
                0,
                MidpointRounding.AwayFromZero);
        }

        // =========================================================
        // TÌM VÀ KIỂM TRA MÃ GIẢM GIÁ
        // =========================================================
        private MaGiamGia KiemTraMaGiamGia(
            string maCode,
            decimal tongTien,
            DateTime ngayDat,
            out string loi)
        {
            loi = "";

            if (string.IsNullOrWhiteSpace(maCode))
            {
                loi = "Vui lòng nhập mã giảm giá.";
                return null;
            }

            maCode = maCode.Trim().ToUpper();

            var maGiamGia = db.MaGiamGias
                .FirstOrDefault(x =>
                    x.MaCode.ToUpper() == maCode);

            if (maGiamGia == null)
            {
                loi = "Mã giảm giá không tồn tại.";
                return null;
            }

            // Trạng thái:
            // 1 = đang hoạt động
            if (maGiamGia.TrangThai.HasValue &&
                maGiamGia.TrangThai.Value != 1)
            {
                loi = "Mã giảm giá hiện không hoạt động.";
                return null;
            }

            // Kiểm tra thời gian áp dụng
            if (ngayDat.Date < maGiamGia.NgayBatDau.Date)
            {
                loi = "Mã giảm giá chưa bắt đầu áp dụng.";
                return null;
            }

            if (ngayDat.Date > maGiamGia.NgayKetThuc.Date)
            {
                loi = "Mã giảm giá đã hết hạn.";
                return null;
            }

            // Kiểm tra số lượng
            if (maGiamGia.SoLuong <= 0)
            {
                loi = "Mã giảm giá đã hết lượt sử dụng.";
                return null;
            }

            // Kiểm tra giá trị đơn hàng tối thiểu
            if (maGiamGia.GiaTriDonHangToiTheu.HasValue &&
                tongTien < maGiamGia.GiaTriDonHangToiTheu.Value)
            {
                loi =
                    "Đơn hàng phải có giá trị từ " +
                    maGiamGia.GiaTriDonHangToiTheu.Value
                        .ToString("N0") +
                    " VNĐ để sử dụng mã này.";

                return null;
            }

            return maGiamGia;
        }

        // =========================================================
        // HOÀN LẠI DỊCH VỤ KHI ĐƠN BỊ HỦY
        // =========================================================
        private void HoanLaiDichVu(DatSan datSan)
        {
            if (datSan == null ||
                datSan.ChiTietDichVuDats == null)
            {
                return;
            }

            foreach (var chiTiet in datSan.ChiTietDichVuDats)
            {
                var dichVu = db.DichVus
                    .FirstOrDefault(d =>
                        d.MaDichVu == chiTiet.MaDichVu);

                if (dichVu != null)
                {
                    dichVu.SoLuongTon += chiTiet.SoLuong;
                }
            }
        }

        // =========================================================
        // GỬI THÔNG BÁO CHO TOÀN BỘ NHÂN VIÊN
        // =========================================================
        private void GuiThongBaoChoNhanVien(
            string tieuDe,
            string noiDung,
            string duongDan = null)
        {
            var danhSachNhanVien = db.NguoiDungs
                .Where(n => n.VaiTro == "NhanVien")
                .ToList();

            foreach (var nhanVien in danhSachNhanVien)
            {
                TaoThongBao(
                    nhanVien.MaNguoiDung,
                    tieuDe,
                    noiDung,
                    duongDan);
            }
        }

        // =========================================================
        // HỦY ĐƠN HẾT HẠN
        // =========================================================
        private void HuyDonHetHan()
        {
            try
            {
                DateTime now = DateTime.Now;

                var dsHetHan = db.DatSans
                    .Include(d => d.ChiTietDichVuDats)
                    .Where(d =>
                        d.TrangThai == "Chờ thanh toán" &&
                        d.HanThanhToan != null &&
                        d.HanThanhToan < now)
                    .ToList();

                if (!dsHetHan.Any())
                    return;

                foreach (var don in dsHetHan)
                {
                    // Hoàn lại dịch vụ đã giữ trong kho
                    HoanLaiDichVu(don);

                    don.TrangThai = "Đã hủy";
                    don.TrangThaiThanhToan = "Hết hạn";
                    don.NgayHuy = now;
                    don.LyDoHuy = "Đơn tự động hủy do quá hạn thanh toán.";
                    don.PhiHuy = 0;
                    don.SoTienHoan = 0;
                    don.TrangThaiHoanTien = "Không áp dụng";
                }

                db.SaveChanges();
            }
            catch
            {
                // Không làm gián đoạn trang đặt sân
            }
        }

        // =========================================================
        // HỦY ĐƠN HẾT HẠN TRONG TRANSACTION
        // =========================================================
        private void HuyDonHetHanTrongTransaction()
        {
            HuyDonHetHan();
        }

        // =========================================================
        // TRANG THÁI / LỊCH SỬ ĐẶT SÂN
        // =========================================================
        [HttpGet]
        [Route("DatSan/TrangThai")]
        public ActionResult TrangThai()
        {
            if (!DaDangNhap())
            {
                return RedirectToAction(
                    "DangNhap",
                    "TaiKhoan");
            }

            HuyDonHetHan();

            int? maNguoiDung = LayMaNguoiDung();
            string vaiTro = LayVaiTro();

            List<DatSan> danhSachDatSan =
                new List<DatSan>();

            if (maNguoiDung.HasValue)
            {
                if (vaiTro == "Admin" ||
                    vaiTro == "NhanVien")
                {
                    danhSachDatSan = db.DatSans
                        .Include(d => d.SanBong)
                        .Include(d => d.KhungGio)
                        .Include(d => d.NguoiDung)
                        .Include(d =>
                            d.ChiTietDichVuDats
                                .Select(c => c.DichVu))
                        .Include(d => d.MaGiamGia)
                        .OrderByDescending(d => d.NgayDat)
                        .ThenByDescending(d => d.MaDatSan)
                        .ToList();
                }
                else
                {
                    danhSachDatSan = db.DatSans
                        .Include(d => d.SanBong)
                        .Include(d => d.KhungGio)
                        .Include(d => d.NguoiDung)
                        .Include(d =>
                            d.ChiTietDichVuDats
                                .Select(c => c.DichVu))
                        .Include(d => d.MaGiamGia)
                        .Where(d =>
                            d.MaNguoiDung ==
                            maNguoiDung.Value)
                        .OrderByDescending(d => d.NgayDat)
                        .ThenByDescending(d => d.MaDatSan)
                        .ToList();
                }
            }

            return View(danhSachDatSan);
        }

        // =========================================================
        // KHÁCH HÀNG HỦY / GỬI YÊU CẦU HỦY ĐƠN ĐẶT SÂN
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult HuyDonKhachHang(int id, string lyDo)
        {
            // 1. Kiểm tra đăng nhập và vai trò
            if (!DaDangNhap())
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            if (LayVaiTro() != "KhachHang")
            {
                return new HttpStatusCodeResult(403);
            }

            int? maNguoiDung = LayMaNguoiDung();

            if (!maNguoiDung.HasValue)
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            if (string.IsNullOrWhiteSpace(lyDo))
            {
                lyDo = "Khách hàng chủ động yêu cầu hủy.";
            }

            lyDo = lyDo.Trim();

            if (lyDo.Length > 500)
            {
                lyDo = lyDo.Substring(0, 500);
            }

            // 2. Chỉ lấy đúng đơn thuộc khách hàng đang đăng nhập
            var datSan = db.DatSans
                .Include(d => d.SanBong)
                .Include(d => d.KhungGio)
                .Include(d =>
                    d.ChiTietDichVuDats
                        .Select(c => c.DichVu))
                .FirstOrDefault(d =>
                    d.MaDatSan == id &&
                    d.MaNguoiDung == maNguoiDung.Value);

            if (datSan == null)
            {
                TempData["Loi"] =
                    "Không tìm thấy đơn đặt sân hoặc bạn không có quyền hủy đơn này.";

                return RedirectToAction("TrangThai");
            }

            // 3. Kiểm tra các trạng thái không được hủy
            if (datSan.TrangThai == "Đã hủy")
            {
                TempData["Loi"] = "Đơn đặt sân này đã được hủy.";
                return RedirectToAction("TrangThai");
            }

            if (datSan.TrangThai == "Yêu cầu hủy")
            {
                TempData["Loi"] =
                    "Bạn đã gửi yêu cầu hủy cho đơn này. Vui lòng chờ Nhân viên xử lý.";

                return RedirectToAction("TrangThai");
            }

            if (datSan.TrangThai == "Hoàn thành")
            {
                TempData["Loi"] = "Đơn đã hoàn thành nên không thể hủy.";
                return RedirectToAction("TrangThai");
            }

            if (datSan.TrangThai == "Đang sử dụng")
            {
                TempData["Loi"] = "Sân đang được sử dụng nên không thể hủy.";
                return RedirectToAction("TrangThai");
            }

            if (datSan.KhungGio == null)
            {
                TempData["Loi"] = "Không xác định được khung giờ của đơn.";
                return RedirectToAction("TrangThai");
            }

            // 4. Tính số giờ còn lại trước khi sân bắt đầu
            DateTime thoiGianBatDau =
                datSan.NgayDat.Date
                    .Add(datSan.KhungGio.GioBatDau);

            DateTime now = DateTime.Now;
            double soGioConLai =
                (thoiGianBatDau - now).TotalHours;

            if (soGioConLai <= 0)
            {
                TempData["Loi"] =
                    "Đơn đã đến giờ sử dụng sân nên không thể hủy.";

                return RedirectToAction("TrangThai");
            }

            // 5. Xác định đơn đã có giao dịch thanh toán hay chưa
            bool daThanhToan =
                datSan.TrangThaiThanhToan == "Đã thanh toán";

            bool dangChoXacNhanTien =
                datSan.TrangThaiThanhToan == "Chờ xác nhận";

            bool daChuyenKhoanDuLieuCu =
                datSan.TrangThaiThanhToan == "Đã chuyển khoản";

            bool xemNhuDaThanhToan =
                daThanhToan ||
                dangChoXacNhanTien ||
                daChuyenKhoanDuLieuCu;

            // =====================================================
            // ĐƠN CHƯA THANH TOÁN
            // - >= 2 giờ: khách tự hủy, không mất phí
            // - < 2 giờ: không cho tự hủy
            // =====================================================
            if (!xemNhuDaThanhToan)
            {
                if (soGioConLai < 2)
                {
                    TempData["Loi"] =
                        "Đơn chỉ còn dưới 2 giờ trước giờ đá. " +
                        "Bạn không thể tự hủy. Vui lòng liên hệ Nhân viên để được hỗ trợ.";

                    return RedirectToAction("TrangThai");
                }

                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        HoanLaiDichVu(datSan);

                        datSan.TrangThai = "Đã hủy";
                        datSan.TrangThaiThanhToan = "Đã hủy";
                        datSan.NgayYeuCauHuy = now;
                        datSan.NgayHuy = now;
                        datSan.LyDoHuy = lyDo;
                        datSan.PhiHuy = 0;
                        datSan.SoTienHoan = 0;
                        datSan.TrangThaiHoanTien = "Không áp dụng";
                        datSan.HanThanhToan = null;

                        TaoThongBao(
                            datSan.MaNguoiDung,
                            "Hủy đơn đặt sân thành công",
                            "Đơn #" + datSan.MaDatSan +
                            " - " + datSan.SanBong.TenSan +
                            " đã được hủy thành công. " +
                            "Đơn chưa thanh toán nên không phát sinh phí hủy.",
                            Url.Action(
                                "ChiTiet",
                                "DatSan",
                                new { id = datSan.MaDatSan })
                        );

                        GuiThongBaoChoNhanVien(
                            "Khách hàng đã hủy đơn",
                            "Khách hàng đã hủy đơn #" +
                            datSan.MaDatSan +
                            " - " + datSan.SanBong.TenSan +
                            ". Lý do: " + lyDo,
                            Url.Action(
                                "ChiTiet",
                                "DatSan",
                                new { id = datSan.MaDatSan })
                        );

                        db.SaveChanges();
                        transaction.Commit();

                        TempData["ThanhCong"] =
                            "Hủy đơn đặt sân thành công. " +
                            "Đơn chưa thanh toán nên bạn không mất phí.";

                        return RedirectToAction("TrangThai");
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

                        TempData["Loi"] =
                            "Không thể hủy đơn. Vui lòng thử lại.";

                        return RedirectToAction("TrangThai");
                    }
                }
            }

            // =====================================================
            // ĐƠN ĐÃ THANH TOÁN / ĐANG CHỜ XÁC NHẬN
            // Không hủy ngay, chỉ gửi yêu cầu cho Nhân viên
            // >= 24 giờ : phí 0%
            // 2 - <24h   : phí 20%
            // < 2 giờ    : phí dự kiến 50%
            // =====================================================
            decimal tyLePhiHuy;

            if (soGioConLai >= 24)
            {
                tyLePhiHuy = 0m;
            }
            else if (soGioConLai >= 2)
            {
                tyLePhiHuy = 0.20m;
            }
            else
            {
                tyLePhiHuy = 0.50m;
            }

            decimal tongTien = datSan.TongTien ?? 0m;
            decimal phiHuy = Math.Round(
                tongTien * tyLePhiHuy,
                0,
                MidpointRounding.AwayFromZero);

            decimal soTienHoan = tongTien - phiHuy;

            if (soTienHoan < 0)
            {
                soTienHoan = 0;
            }

            datSan.TrangThai = "Yêu cầu hủy";
            datSan.NgayYeuCauHuy = now;
            datSan.NgayHuy = null;
            datSan.LyDoHuy = lyDo;
            datSan.PhiHuy = phiHuy;
            datSan.SoTienHoan = soTienHoan;
            datSan.TrangThaiHoanTien = "Chờ xử lý";

            string noiDungKhach;

            if (tyLePhiHuy == 0)
            {
                noiDungKhach =
                    "Yêu cầu hủy đơn #" + datSan.MaDatSan +
                    " đã được gửi. Nếu được Nhân viên chấp nhận, " +
                    "bạn được hoàn 100% (" +
                    soTienHoan.ToString("N0") + " VNĐ).";
            }
            else
            {
                noiDungKhach =
                    "Yêu cầu hủy đơn #" + datSan.MaDatSan +
                    " đã được gửi. Phí hủy dự kiến: " +
                    (tyLePhiHuy * 100).ToString("0") + "% (" +
                    phiHuy.ToString("N0") + " VNĐ). " +
                    "Số tiền hoàn dự kiến: " +
                    soTienHoan.ToString("N0") + " VNĐ.";
            }

            TaoThongBao(
                datSan.MaNguoiDung,
                "Đã gửi yêu cầu hủy đơn",
                noiDungKhach,
                Url.Action(
                    "ChiTiet",
                    "DatSan",
                    new { id = datSan.MaDatSan })
            );

            GuiThongBaoChoNhanVien(
                "Có yêu cầu hủy đơn mới",
                "Khách hàng yêu cầu hủy đơn #" +
                datSan.MaDatSan +
                " - " + datSan.SanBong.TenSan +
                ". Lý do: " + lyDo +
                ". Phí hủy dự kiến: " +
                phiHuy.ToString("N0") +
                " VNĐ. Số tiền hoàn dự kiến: " +
                soTienHoan.ToString("N0") + " VNĐ.",
                Url.Action(
                    "ChiTiet",
                    "DatSan",
                    new { id = datSan.MaDatSan })
            );

            db.SaveChanges();

            TempData["ThanhCong"] =
                "Đã gửi yêu cầu hủy đến Nhân viên. " +
                "Phí hủy dự kiến: " +
                phiHuy.ToString("N0") +
                " VNĐ. Số tiền hoàn dự kiến: " +
                soTienHoan.ToString("N0") + " VNĐ.";

            return RedirectToAction("TrangThai");
        }

        // =========================================================
        // NHÂN VIÊN DUYỆT YÊU CẦU HỦY
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DuyetYeuCauHuy(int id)
        {
            if (!DaDangNhap())
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            // Theo phân quyền đã chốt: chỉ Nhân viên trực tiếp xử lý hủy.
            if (LayVaiTro() != "NhanVien")
            {
                return new HttpStatusCodeResult(403);
            }

            var datSan = db.DatSans
                .Include(d => d.SanBong)
                .Include(d => d.KhungGio)
                .Include(d => d.NguoiDung)
                .Include(d =>
                    d.ChiTietDichVuDats
                        .Select(c => c.DichVu))
                .FirstOrDefault(d => d.MaDatSan == id);

            if (datSan == null)
            {
                return HttpNotFound();
            }

            if (datSan.TrangThai != "Yêu cầu hủy")
            {
                TempData["Loi"] =
                    "Đơn này không có yêu cầu hủy đang chờ xử lý.";

                return RedirectToAction("TrangThai");
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // Khi Nhân viên chấp nhận hủy thì mới hoàn dịch vụ về kho.
                    HoanLaiDichVu(datSan);

                    datSan.TrangThai = "Đã hủy";
                    datSan.NgayHuy = DateTime.Now;
                    datSan.HanThanhToan = null;

                    decimal soTienHoan =
                        datSan.SoTienHoan ?? 0m;

                    bool coGiaoDichThanhToan =
                        datSan.TrangThaiThanhToan == "Đã thanh toán" ||
                        datSan.TrangThaiThanhToan == "Chờ xác nhận" ||
                        datSan.TrangThaiThanhToan == "Đã chuyển khoản";

                    if (coGiaoDichThanhToan)
                    {
                        datSan.TrangThaiHoanTien =
                            soTienHoan > 0
                                ? "Chờ hoàn tiền"
                                : "Không hoàn tiền";
                    }
                    else
                    {
                        datSan.TrangThaiHoanTien = "Không áp dụng";
                    }

                    string tenSan =
                        datSan.SanBong != null
                            ? datSan.SanBong.TenSan
                            : "sân đã đặt";

                    string noiDung =
                        "Yêu cầu hủy đơn #" +
                        datSan.MaDatSan +
                        " - " + tenSan +
                        " đã được Nhân viên chấp nhận. " +
                        "Phí hủy: " +
                        (datSan.PhiHuy ?? 0m).ToString("N0") +
                        " VNĐ. Số tiền hoàn dự kiến: " +
                        soTienHoan.ToString("N0") +
                        " VNĐ.";

                    if (datSan.TrangThaiHoanTien == "Chờ hoàn tiền")
                    {
                        noiDung +=
                            " Khoản hoàn tiền đang chờ Nhân viên xử lý.";
                    }

                    TaoThongBao(
                        datSan.MaNguoiDung,
                        "Yêu cầu hủy đã được chấp nhận",
                        noiDung,
                        Url.Action(
                            "ChiTiet",
                            "DatSan",
                            new { id = datSan.MaDatSan })
                    );

                    db.SaveChanges();
                    transaction.Commit();

                    TempData["ThanhCong"] =
                        "Đã chấp nhận yêu cầu hủy đơn #" +
                        datSan.MaDatSan + ".";
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

                    TempData["Loi"] =
                        "Không thể xử lý yêu cầu hủy. Vui lòng thử lại.";
                }
            }

            return RedirectToAction("TrangThai");
        }

        // =========================================================
        // NHÂN VIÊN TỪ CHỐI YÊU CẦU HỦY
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult TuChoiYeuCauHuy(
            int id,
            string lyDoTuChoi)
        {
            if (!DaDangNhap())
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            if (LayVaiTro() != "NhanVien")
            {
                return new HttpStatusCodeResult(403);
            }

            if (string.IsNullOrWhiteSpace(lyDoTuChoi))
            {
                lyDoTuChoi =
                    "Yêu cầu hủy không đáp ứng điều kiện xử lý.";
            }

            lyDoTuChoi = lyDoTuChoi.Trim();

            if (lyDoTuChoi.Length > 500)
            {
                lyDoTuChoi = lyDoTuChoi.Substring(0, 500);
            }

            var datSan = db.DatSans
                .Include(d => d.SanBong)
                .FirstOrDefault(d => d.MaDatSan == id);

            if (datSan == null)
            {
                return HttpNotFound();
            }

            if (datSan.TrangThai != "Yêu cầu hủy")
            {
                TempData["Loi"] =
                    "Đơn này không có yêu cầu hủy đang chờ xử lý.";

                return RedirectToAction("TrangThai");
            }

            // Khôi phục trạng thái đơn trước yêu cầu hủy dựa theo trạng thái thanh toán.
            if (datSan.TrangThaiThanhToan == "Đã thanh toán")
            {
                datSan.TrangThai = "Đã xác nhận";
            }
            else if (
                datSan.TrangThaiThanhToan == "Chờ xác nhận" ||
                datSan.TrangThaiThanhToan == "Đã chuyển khoản" ||
                datSan.TrangThaiThanhToan == "Thanh toán trực tiếp tại sân")
            {
                datSan.TrangThai = "Chờ xác nhận";
            }
            else
            {
                datSan.TrangThai = "Chờ thanh toán";
            }

            // Xóa dữ liệu của yêu cầu hủy hiện tại để khách có thể tiếp tục sử dụng đơn.
            datSan.NgayYeuCauHuy = null;
            datSan.NgayHuy = null;
            datSan.LyDoHuy = null;
            datSan.PhiHuy = null;
            datSan.SoTienHoan = null;
            datSan.TrangThaiHoanTien = null;

            string tenSan =
                datSan.SanBong != null
                    ? datSan.SanBong.TenSan
                    : "sân đã đặt";

            TaoThongBao(
                datSan.MaNguoiDung,
                "Yêu cầu hủy bị từ chối",
                "Yêu cầu hủy đơn #" +
                datSan.MaDatSan +
                " - " + tenSan +
                " đã bị Nhân viên từ chối. Lý do: " +
                lyDoTuChoi,
                Url.Action(
                    "ChiTiet",
                    "DatSan",
                    new { id = datSan.MaDatSan })
            );

            db.SaveChanges();

            TempData["ThanhCong"] =
                "Đã từ chối yêu cầu hủy đơn #" +
                datSan.MaDatSan + ".";

            return RedirectToAction("TrangThai");
        }

        // =========================================================
        // NHÂN VIÊN XÁC NHẬN ĐÃ HOÀN TIỀN
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult XacNhanHoanTien(int id)
        {
            if (!DaDangNhap())
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            if (LayVaiTro() != "NhanVien")
            {
                return new HttpStatusCodeResult(403);
            }

            var datSan = db.DatSans
                .Include(d => d.SanBong)
                .FirstOrDefault(d => d.MaDatSan == id);

            if (datSan == null)
            {
                return HttpNotFound();
            }

            if (datSan.TrangThai != "Đã hủy" ||
                datSan.TrangThaiHoanTien != "Chờ hoàn tiền")
            {
                TempData["Loi"] =
                    "Đơn này không có khoản hoàn tiền đang chờ xử lý.";

                return RedirectToAction("TrangThai");
            }

            datSan.TrangThaiHoanTien = "Đã hoàn tiền";

            string tenSan =
                datSan.SanBong != null
                    ? datSan.SanBong.TenSan
                    : "sân đã đặt";

            TaoThongBao(
                datSan.MaNguoiDung,
                "Đã hoàn tiền đơn hủy",
                "Nhân viên đã xác nhận hoàn " +
                (datSan.SoTienHoan ?? 0m).ToString("N0") +
                " VNĐ cho đơn #" +
                datSan.MaDatSan +
                " - " + tenSan + ".",
                Url.Action(
                    "ChiTiet",
                    "DatSan",
                    new { id = datSan.MaDatSan })
            );

            db.SaveChanges();

            TempData["ThanhCong"] =
                "Đã xác nhận hoàn tiền cho đơn #" +
                datSan.MaDatSan + ".";

            return RedirectToAction("TrangThai");
        }

        // =========================================================
        // TRANG ĐẶT SÂN
        // =========================================================
        public ActionResult Index()
        {
            if (!DaDangNhap())
            {
                return RedirectToAction(
                    "DangNhap",
                    "TaiKhoan");
            }

            HuyDonHetHan();

            var dsSan = db.SanBongs
                .Where(s =>
                    s.TrangThai == "Hoạt động")
                .OrderBy(s => s.MaSan)
                .ToList();

            var dsKhungGio = db.KhungGios
                .OrderBy(k => k.GioBatDau)
                .ToList();

            var dsDichVu = db.DichVus
                .Where(d => d.SoLuongTon > 0)
                .OrderBy(d => d.MaDichVu)
                .ToList();

            int? maNguoiDung =
                LayMaNguoiDung();

            var dsDonChoThanhToan =
                new List<DatSan>();

            if (maNguoiDung.HasValue)
            {
                dsDonChoThanhToan =
                    db.DatSans
                    .Include(d => d.SanBong)
                    .Include(d => d.KhungGio)
                    .Include(d =>
                        d.ChiTietDichVuDats
                            .Select(c => c.DichVu))
                    .Include(d => d.MaGiamGia)
                    .Where(d =>
                        d.MaNguoiDung ==
                            maNguoiDung.Value &&
                        d.TrangThai ==
                            "Chờ thanh toán")
                    .OrderBy(d => d.NgayDat)
                    .ThenBy(d => d.MaDatSan)
                    .ToList();
            }

            ViewBag.KhungGio = dsKhungGio;
            ViewBag.DichVu = dsDichVu;
            ViewBag.DonChoThanhToan =
                dsDonChoThanhToan;


            // Giới hạn ngày được phép chọn trên giao diện.
            // View có thể dùng 2 giá trị này làm min/max cho input type=date.
            ViewBag.NgayDatToiThieu =
                DateTime.Today.ToString("yyyy-MM-dd");

            ViewBag.NgayDatToiDa =
                DateTime.Today
                    .AddDays(SO_NGAY_DAT_TRUOC_TOI_DA)
                    .ToString("yyyy-MM-dd");

            return View(dsSan);
        }

        // =========================================================
        // LẤY LỊCH TRỐNG THEO NGÀY + THỜI LƯỢNG
        // Giao diện mới hiển thị các mốc 30 phút từ 06:00 đến 23:00.
        // =========================================================
        [HttpGet]
        public JsonResult LayLichTrong(
            int maSan,
            string ngayDat,
            int soPhut = 60)
        {
            if (!DaDangNhap())
            {
                return Json(
                    new
                    {
                        success = false,
                        message = "Phiên đăng nhập đã hết. Vui lòng đăng nhập lại."
                    },
                    JsonRequestBehavior.AllowGet);
            }

            DateTime ngay;

            if (!DateTime.TryParse(ngayDat, out ngay))
            {
                return Json(
                    new
                    {
                        success = false,
                        message = "Ngày đặt sân không hợp lệ."
                    },
                    JsonRequestBehavior.AllowGet);
            }

            ngay = ngay.Date;

            DateTime homNay = DateTime.Today;
            DateTime ngayToiDa =
                homNay.AddDays(SO_NGAY_DAT_TRUOC_TOI_DA);

            if (ngay < homNay)
            {
                return Json(
                    new
                    {
                        success = false,
                        message = "Không thể đặt sân trong ngày đã qua."
                    },
                    JsonRequestBehavior.AllowGet);
            }

            if (ngay > ngayToiDa)
            {
                return Json(
                    new
                    {
                        success = false,
                        message =
                            "Bạn chỉ được đặt sân trước tối đa " +
                            SO_NGAY_DAT_TRUOC_TOI_DA +
                            " ngày."
                    },
                    JsonRequestBehavior.AllowGet);
            }

            // Chỉ cho 3 lựa chọn giống giao diện minh họa.
            if (soPhut != 60 &&
                soPhut != 90 &&
                soPhut != 120)
            {
                return Json(
                    new
                    {
                        success = false,
                        message = "Thời lượng chỉ được chọn 1 giờ, 1.5 giờ hoặc 2 giờ."
                    },
                    JsonRequestBehavior.AllowGet);
            }

            var san = db.SanBongs
                .FirstOrDefault(s =>
                    s.MaSan == maSan &&
                    s.TrangThai == "Hoạt động");

            if (san == null)
            {
                return Json(
                    new
                    {
                        success = false,
                        message = "Sân không tồn tại hoặc đang ngừng hoạt động."
                    },
                    JsonRequestBehavior.AllowGet);
            }

            decimal donGiaTheoGio =
                san.DonGiaTheoGio ?? 0m;

            if (donGiaTheoGio <= 0)
            {
                return Json(
                    new
                    {
                        success = false,
                        message = "Sân này chưa được cấu hình đơn giá theo giờ."
                    },
                    JsonRequestBehavior.AllowGet);
            }

            HuyDonHetHan();

            var dsDonTrongNgay = db.DatSans
                .Include(d => d.KhungGio)
                .Where(d =>
                    d.MaSan == maSan &&
                    DbFunctions.TruncateTime(d.NgayDat) == ngay &&
                    d.TrangThai != "Đã hủy")
                .ToList();

            TimeSpan gioMoCua = new TimeSpan(6, 0, 0);
            TimeSpan gioDongCua = new TimeSpan(23, 0, 0);
            TimeSpan buoc = TimeSpan.FromMinutes(30);
            TimeSpan doDai = TimeSpan.FromMinutes(soPhut);

            var slots = new List<object>();

            for (TimeSpan batDau = gioMoCua;
                 batDau + doDai <= gioDongCua;
                 batDau = batDau.Add(buoc))
            {
                TimeSpan ketThuc = batDau.Add(doDai);
                DateTime thoiDiemBatDau = ngay.Add(batDau);

                bool daQua =
                    thoiDiemBatDau <= DateTime.Now;

                bool biTrung = dsDonTrongNgay.Any(d =>
                {
                    if (d.KhungGio == null)
                        return false;

                    TimeSpan bdCu = d.KhungGio.GioBatDau;
                    TimeSpan ktCu = d.KhungGio.GioKetThuc;

                    // Hai khoảng thời gian trùng nhau khi:
                    // bắt đầu mới < kết thúc cũ và kết thúc mới > bắt đầu cũ.
                    return batDau < ktCu && ketThuc > bdCu;
                });

                string lyDo = "";

                if (daQua)
                    lyDo = "Đã qua";
                else if (biTrung)
                    lyDo = "Đã đặt";

                slots.Add(new
                {
                    gioBatDau = batDau.ToString(@"hh\:mm"),
                    gioKetThuc = ketThuc.ToString(@"hh\:mm"),
                    coTheDat = !daQua && !biTrung,
                    lyDo = lyDo
                });
            }

            decimal thoiLuong = (decimal)soPhut / 60m;

            decimal giaDuKien = Math.Round(
                donGiaTheoGio * thoiLuong,
                0,
                MidpointRounding.AwayFromZero);

            return Json(
                new
                {
                    success = true,
                    maSan = san.MaSan,
                    tenSan = san.TenSan,
                    loaiSan = san.LoaiSan,
                    donGiaTheoGio = donGiaTheoGio,
                    thoiLuong = thoiLuong,
                    giaDuKien = giaDuKien,
                    slots = slots
                },
                JsonRequestBehavior.AllowGet);
        }

        // =========================================================
        // LẤY GIÁ SÂN
        // =========================================================
        [HttpGet]
        public JsonResult LayGiaSan(
            int maSan,
            int maKhungGio)
        {
            var gia =
                db.GiaTheoKhungGios
                .FirstOrDefault(g =>
                    g.MaSan == maSan &&
                    g.MaKhungGio == maKhungGio);

            if (gia == null)
            {
                return Json(
                    new
                    {
                        success = false,
                        message =
                            "Chưa có giá cho sân này."
                    },
                    JsonRequestBehavior.AllowGet);
            }

            return Json(
                new
                {
                    success = true,
                    gia = gia.DonGia
                },
                JsonRequestBehavior.AllowGet);
        }

        // =========================================================
        // KIỂM TRA SÂN
        // =========================================================
        [HttpGet]
        public JsonResult KiemTraSan(
            int maSan,
            int maKhungGio,
            string ngayDat)
        {
            DateTime ngay;

            if (!DateTime.TryParse(
                ngayDat,
                out ngay))
            {
                return Json(
                    new
                    {
                        success = false,
                        message =
                            "Ngày đặt không hợp lệ."
                    },
                    JsonRequestBehavior.AllowGet);
            }

            ngay = ngay.Date;

            // Không cho kiểm tra/đặt sân ở ngày đã qua
            if (ngay < DateTime.Today)
            {
                return Json(
                    new
                    {
                        success = false,
                        quaGio = true,
                        message =
                            "Không thể đặt sân trong ngày đã qua."
                    },
                    JsonRequestBehavior.AllowGet);
            }

            // Không cho đặt quá xa. Mặc định chỉ mở lịch tối đa 7 ngày.
            DateTime ngayDatToiDa =
                DateTime.Today.AddDays(SO_NGAY_DAT_TRUOC_TOI_DA);

            if (ngay > ngayDatToiDa)
            {
                return Json(
                    new
                    {
                        success = false,
                        quaXa = true,
                        message =
                            "Bạn chỉ có thể đặt sân trước tối đa " +
                            SO_NGAY_DAT_TRUOC_TOI_DA +
                            " ngày."
                    },
                    JsonRequestBehavior.AllowGet);
            }

            // Lấy khung giờ để kiểm tra thời điểm bắt đầu thực tế
            var khungGio = db.KhungGios
                .FirstOrDefault(k =>
                    k.MaKhungGio == maKhungGio);

            if (khungGio == null)
            {
                return Json(
                    new
                    {
                        success = false,
                        message =
                            "Khung giờ không tồn tại."
                    },
                    JsonRequestBehavior.AllowGet);
            }

            DateTime thoiGianBatDau =
                ngay.Add(khungGio.GioBatDau);

            // Nếu đặt trong hôm nay thì giờ bắt đầu phải còn ở tương lai.
            // Dùng <= để đúng cả trường hợp vừa đúng giờ bắt đầu.
            if (thoiGianBatDau <= DateTime.Now)
            {
                return Json(
                    new
                    {
                        success = false,
                        quaGio = true,
                        message =
                            "Khung giờ này đã bắt đầu hoặc đã qua. Vui lòng chọn khung giờ khác."
                    },
                    JsonRequestBehavior.AllowGet);
            }

            HuyDonHetHan();

            int? maNguoiDung =
                LayMaNguoiDung();

            var donDaDat =
                db.DatSans
                .FirstOrDefault(d =>
                    d.MaSan == maSan &&
                    d.MaKhungGio == maKhungGio &&
                    DbFunctions.TruncateTime(
                        d.NgayDat) == ngay &&
                    d.TrangThai != "Đã hủy");

            if (donDaDat == null)
            {
                return Json(
                    new
                    {
                        success = true,
                        daDat = false,
                        cuaToi = false,
                        maDatSan = 0
                    },
                    JsonRequestBehavior.AllowGet);
            }

            if (
                maNguoiDung.HasValue &&
                donDaDat.MaNguoiDung ==
                    maNguoiDung.Value &&
                donDaDat.TrangThai ==
                    "Chờ thanh toán")
            {
                return Json(
                    new
                    {
                        success = true,
                        daDat = true,
                        cuaToi = true,
                        maDatSan =
                            donDaDat.MaDatSan,
                        message =
                            "Bạn đang giữ sân này để thanh toán."
                    },
                    JsonRequestBehavior.AllowGet);
            }

            return Json(
                new
                {
                    success = true,
                    daDat = true,
                    cuaToi = false,
                    maDatSan =
                        donDaDat.MaDatSan,
                    message =
                        "Sân này đã được đặt trong khung giờ này."
                },
                JsonRequestBehavior.AllowGet);
        }

        // =========================================================
        // ÁP DỤNG MÃ GIẢM GIÁ
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult ApDungMaGiamGia(
            string maCode,
            decimal tongTien,
            string ngayDat)
        {
            try
            {
                if (!DaDangNhap())
                {
                    return Json(new
                    {
                        success = false,
                        message =
                            "Phiên đăng nhập đã hết. Vui lòng đăng nhập lại."
                    });
                }

                DateTime ngay;

                if (!DateTime.TryParse(
                    ngayDat,
                    out ngay))
                {
                    ngay = DateTime.Today;
                }

                ngay = ngay.Date;

                if (tongTien < 0)
                    tongTien = 0;

                string loi;

                var maGiamGia =
                    KiemTraMaGiamGia(
                        maCode,
                        tongTien,
                        ngay,
                        out loi);

                if (maGiamGia == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = loi
                    });
                }

                decimal tienGiam =
                    TinhTienGiam(
                        maGiamGia,
                        tongTien);

                decimal tongSauGiam =
                    tongTien - tienGiam;

                if (tongSauGiam < 0)
                    tongSauGiam = 0;

                return Json(new
                {
                    success = true,

                    maGiamGiaID =
                        maGiamGia.MaGiamGiaID,

                    maCode =
                        maGiamGia.MaCode,

                    tienGiam = tienGiam,

                    tongTien =
                        tongTien,

                    tongSauGiam =
                        tongSauGiam,

                    phanTramGiam =
                        maGiamGia.PhanTramGiam,

                    soTienGiam =
                        maGiamGia.SoTienGiam
                });
            }
            catch
            {
                return Json(new
                {
                    success = false,
                    message =
                        "Không thể áp dụng mã giảm giá. Vui lòng thử lại."
                });
            }
        }

        // =========================================================
        // XÁC NHẬN ĐẶT SÂN - KHÁCH TỰ CHỌN GIỜ
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult XacNhanDatSan(
            FormCollection form,
            List<ItemSanChonModel> dsChon)
        {
            int? maNguoiDungNullable = LayMaNguoiDung();

            if (!maNguoiDungNullable.HasValue)
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            string vaiTro = LayVaiTro();

            // Mặc định: khách tự đặt thì đơn thuộc về tài khoản đang đăng nhập.
            int? maNguoiDungDatSan =
                maNguoiDungNullable.Value;

            string hoTenKhachHang = null;
            string soDienThoaiKhachHang = null;
            string emailKhachHang = null;

            // Riêng Nhân viên: nhập trực tiếp thông tin khách.
            // Khách chưa có tài khoản vẫn tạo đơn được.
            if (vaiTro == "NhanVien")
            {
                hoTenKhachHang =
                    (form["HoTenKhachHang"] ?? "")
                    .Trim();

                soDienThoaiKhachHang =
                    (form["SoDienThoaiKhachHang"] ?? "")
                    .Trim();

                emailKhachHang =
                    (form["EmailKhachHang"] ?? "")
                    .Trim();

                if (string.IsNullOrWhiteSpace(hoTenKhachHang))
                {
                    TempData["Loi"] =
                        "Vui lòng nhập họ tên khách hàng.";

                    return RedirectToAction("Index");
                }

                if (string.IsNullOrWhiteSpace(soDienThoaiKhachHang))
                {
                    TempData["Loi"] =
                        "Vui lòng nhập số điện thoại khách hàng.";

                    return RedirectToAction("Index");
                }

                if (hoTenKhachHang.Length > 100)
                {
                    TempData["Loi"] =
                        "Họ tên khách hàng không được vượt quá 100 ký tự.";

                    return RedirectToAction("Index");
                }

                if (soDienThoaiKhachHang.Length > 20)
                {
                    TempData["Loi"] =
                        "Số điện thoại khách hàng không hợp lệ.";

                    return RedirectToAction("Index");
                }

                if (!string.IsNullOrWhiteSpace(emailKhachHang))
                {
                    if (emailKhachHang.Length > 150)
                    {
                        TempData["Loi"] =
                            "Email khách hàng không được vượt quá 150 ký tự.";

                        return RedirectToAction("Index");
                    }

                    try
                    {
                        var diaChiEmail =
                            new System.Net.Mail.MailAddress(emailKhachHang);
                    }
                    catch
                    {
                        TempData["Loi"] =
                            "Email khách hàng không đúng định dạng.";

                        return RedirectToAction("Index");
                    }
                }

                // Nếu khách đã có tài khoản thì tự liên kết theo SĐT hoặc Email.
                // Nếu chưa có tài khoản thì để MaNguoiDung = NULL (khách vãng lai).
                NguoiDung khachHangCoTaiKhoan = null;

                if (!string.IsNullOrWhiteSpace(emailKhachHang))
                {
                    khachHangCoTaiKhoan =
                        db.NguoiDungs
                        .FirstOrDefault(n =>
                            n.VaiTro == "KhachHang" &&
                            (
                                n.SoDienThoai == soDienThoaiKhachHang ||
                                n.Email == emailKhachHang
                            ));
                }
                else
                {
                    khachHangCoTaiKhoan =
                        db.NguoiDungs
                        .FirstOrDefault(n =>
                            n.VaiTro == "KhachHang" &&
                            n.SoDienThoai == soDienThoaiKhachHang);
                }

                maNguoiDungDatSan =
                    khachHangCoTaiKhoan != null
                        ? (int?)khachHangCoTaiKhoan.MaNguoiDung
                        : null;
            }

            // =====================================================
            // THANH TOÁN KHI NHÂN VIÊN ĐẶT SÂN CHO KHÁCH
            // =====================================================
            // Nhân viên tạo đơn trực tiếp tại quầy.
            // Chỉ cho phép 2 hình thức:
            //   - Tiền mặt
            //   - Chuyển khoản
            //
            // Cả hai đều được xem là đã thanh toán ngay khi tạo đơn.
            string thanhToanNhanVien = null;
            string hinhThucThanhToanNhanVien = null;
            string trangThaiThanhToanNhanVien = null;
            DateTime? ngayThanhToanNhanVien = null;

            if (vaiTro == "NhanVien")
            {
                thanhToanNhanVien =
                    (form["ThanhToanNhanVien"] ??
                     form["HinhThucThanhToanNhanVien"] ??
                     "")
                    .Trim();

                if (string.IsNullOrWhiteSpace(thanhToanNhanVien))
                {
                    TempData["Loi"] =
                        "Vui lòng chọn phương thức thanh toán: Tiền mặt hoặc Chuyển khoản.";

                    return RedirectToAction("Index");
                }

                switch (thanhToanNhanVien)
                {
                    case "TienMat":
                    case "Đã thanh toán tiền mặt":
                        hinhThucThanhToanNhanVien = "Tiền mặt";
                        trangThaiThanhToanNhanVien = "Đã thanh toán";
                        ngayThanhToanNhanVien = DateTime.Now;
                        break;

                    case "ChuyenKhoan":
                    case "Đã thanh toán chuyển khoản":
                        hinhThucThanhToanNhanVien = "Chuyển khoản";
                        trangThaiThanhToanNhanVien = "Đã thanh toán";
                        ngayThanhToanNhanVien = DateTime.Now;
                        break;

                    default:
                        TempData["Loi"] =
                            "Phương thức thanh toán của Nhân viên không hợp lệ.";

                        return RedirectToAction("Index");
                }
            }

            DateTime ngayDat;

            if (!DateTime.TryParse(form["NgayDat"], out ngayDat))
            {
                TempData["Loi"] = "Ngày đặt sân không hợp lệ.";
                return RedirectToAction("Index");
            }

            ngayDat = ngayDat.Date;

            DateTime homNay = DateTime.Today;
            DateTime ngayToiDa =
                homNay.AddDays(SO_NGAY_DAT_TRUOC_TOI_DA);

            if (ngayDat < homNay)
            {
                TempData["Loi"] = "Không thể đặt sân trong ngày đã qua.";
                return RedirectToAction("Index");
            }

            if (ngayDat > ngayToiDa)
            {
                TempData["Loi"] =
                    "Bạn chỉ được đặt sân trước tối đa " +
                    SO_NGAY_DAT_TRUOC_TOI_DA +
                    " ngày.";

                return RedirectToAction("Index");
            }

            if (dsChon == null)
                dsChon = new List<ItemSanChonModel>();

            if (!dsChon.Any())
            {
                TempData["Loi"] = "Vui lòng chọn sân và giờ đặt.";
                return RedirectToAction("Index");
            }

            if (dsChon.Count > 3)
            {
                TempData["Loi"] = "Bạn chỉ được đặt tối đa 3 sân.";
                return RedirectToAction("Index");
            }

            // Kiểm tra trùng lựa chọn trong request theo sân + khoảng giờ.
            bool trungTrongDanhSach = dsChon
                .GroupBy(x => new
                {
                    x.MaSan,
                    GioBatDau = (x.GioBatDau ?? "").Trim(),
                    GioKetThuc = (x.GioKetThuc ?? "").Trim()
                })
                .Any(g => g.Count() > 1);

            if (trungTrongDanhSach)
            {
                TempData["Loi"] = "Bạn đang chọn trùng sân và khoảng thời gian.";
                return RedirectToAction("Index");
            }

            string maCode = form["MaGiamGia"];

            if (string.IsNullOrWhiteSpace(maCode))
                maCode = form["MaCode"];

            maCode = string.IsNullOrWhiteSpace(maCode)
                ? null
                : maCode.Trim().ToUpper();

            using (var transaction =
                db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                try
                {
                    HuyDonHetHanTrongTransaction();

                    // =================================================
                    // CHUẨN HÓA GIỜ + TỰ TẠO KHUNG GIỜ/GIÁ NẾU CHƯA CÓ
                    // =================================================
                    foreach (var item in dsChon)
                    {
                        var san = db.SanBongs
                            .FirstOrDefault(s =>
                                s.MaSan == item.MaSan &&
                                s.TrangThai == "Hoạt động");

                        if (san == null)
                        {
                            transaction.Rollback();
                            TempData["Loi"] =
                                "Có sân không tồn tại hoặc đang ngừng hoạt động.";
                            return RedirectToAction("Index");
                        }

                        decimal donGiaTheoGio =
                            san.DonGiaTheoGio ?? 0m;

                        if (donGiaTheoGio <= 0)
                        {
                            transaction.Rollback();
                            TempData["Loi"] =
                                "Sân \"" + san.TenSan +
                                "\" chưa được cấu hình đơn giá theo giờ.";
                            return RedirectToAction("Index");
                        }

                        TimeSpan gioBatDau;
                        TimeSpan gioKetThuc;

                        if (!TimeSpan.TryParse(item.GioBatDau, out gioBatDau) ||
                            !TimeSpan.TryParse(item.GioKetThuc, out gioKetThuc))
                        {
                            transaction.Rollback();
                            TempData["Loi"] = "Giờ bắt đầu hoặc giờ kết thúc không hợp lệ.";
                            return RedirectToAction("Index");
                        }

                        TimeSpan gioMoCua = new TimeSpan(6, 0, 0);
                        TimeSpan gioDongCua = new TimeSpan(23, 0, 0);

                        if (gioBatDau < gioMoCua ||
                            gioKetThuc > gioDongCua ||
                            gioKetThuc <= gioBatDau)
                        {
                            transaction.Rollback();
                            TempData["Loi"] =
                                "Giờ đặt phải nằm trong thời gian hoạt động 06:00 - 23:00.";
                            return RedirectToAction("Index");
                        }

                        double soPhut =
                            (gioKetThuc - gioBatDau).TotalMinutes;

                        if (soPhut != 60 &&
                            soPhut != 90 &&
                            soPhut != 120)
                        {
                            transaction.Rollback();
                            TempData["Loi"] =
                                "Thời lượng chỉ được chọn 1 giờ, 1.5 giờ hoặc 2 giờ.";
                            return RedirectToAction("Index");
                        }

                        DateTime thoiGianBatDau =
                            ngayDat.Add(gioBatDau);

                        if (thoiGianBatDau <= DateTime.Now)
                        {
                            transaction.Rollback();
                            TempData["Loi"] =
                                "Khung giờ " +
                                gioBatDau.ToString(@"hh\:mm") +
                                " - " +
                                gioKetThuc.ToString(@"hh\:mm") +
                                " đã bắt đầu hoặc đã qua.";
                            return RedirectToAction("Index");
                        }

                        // Kiểm tra giao nhau với mọi đơn hiện có trong ngày.
                        var dsDonCungSan = db.DatSans
                            .Include(d => d.KhungGio)
                            .Where(d =>
                                d.MaSan == item.MaSan &&
                                DbFunctions.TruncateTime(d.NgayDat) == ngayDat &&
                                d.TrangThai != "Đã hủy")
                            .ToList();

                        var donTrung = dsDonCungSan
                            .FirstOrDefault(d =>
                                d.KhungGio != null &&
                                gioBatDau < d.KhungGio.GioKetThuc &&
                                gioKetThuc > d.KhungGio.GioBatDau);

                        if (donTrung != null)
                        {
                            transaction.Rollback();

                            if (maNguoiDungDatSan.HasValue &&
                                donTrung.MaNguoiDung == maNguoiDungDatSan.Value &&
                                donTrung.TrangThai == "Chờ thanh toán")
                            {
                                return RedirectToAction(
                                    "ThanhToan",
                                    new { id = donTrung.MaDatSan });
                            }

                            TempData["Loi"] =
                                "Sân \"" + san.TenSan +
                                "\" đã có người đặt trong khoảng " +
                                gioBatDau.ToString(@"hh\:mm") +
                                " - " +
                                gioKetThuc.ToString(@"hh\:mm") +
                                ". Vui lòng chọn giờ khác.";

                            return RedirectToAction("Index");
                        }

                        // Tìm hoặc tạo KhungGio động để toàn bộ phần thanh toán,
                        // lịch sử và hủy đơn cũ vẫn tiếp tục dùng navigation KhungGio.
                        var khungGio = db.KhungGios
                            .FirstOrDefault(k =>
                                k.GioBatDau == gioBatDau &&
                                k.GioKetThuc == gioKetThuc);

                        if (khungGio == null)
                        {
                            khungGio = new KhungGio
                            {
                                GioBatDau = gioBatDau,
                                GioKetThuc = gioKetThuc
                            };

                            db.KhungGios.Add(khungGio);
                            db.SaveChanges();
                        }

                        item.MaKhungGio = khungGio.MaKhungGio;

                        decimal thoiLuongGio =
                            (decimal)soPhut / 60m;

                        decimal giaTinhTheoGio = Math.Round(
                            donGiaTheoGio * thoiLuongGio,
                            0,
                            MidpointRounding.AwayFromZero);

                        var giaSan = db.GiaTheoKhungGios
                            .FirstOrDefault(g =>
                                g.MaSan == item.MaSan &&
                                g.MaKhungGio == khungGio.MaKhungGio);

                        if (giaSan == null)
                        {
                            giaSan = new GiaTheoKhungGio
                            {
                                MaSan = item.MaSan,
                                MaKhungGio = khungGio.MaKhungGio,
                                DonGia = giaTinhTheoGio
                            };

                            db.GiaTheoKhungGios.Add(giaSan);
                        }
                        else
                        {
                            // Đồng bộ với đơn giá hiện tại của sân.
                            giaSan.DonGia = giaTinhTheoGio;
                        }
                    }

                    db.SaveChanges();

                    // =================================================
                    // TÍNH DỊCH VỤ THEO KHO
                    // =================================================
                    var tongDichVuTheoKho = dsChon
                        .Where(s => s.DichVus != null)
                        .SelectMany(s => s.DichVus)
                        .Where(d => d.SoLuong > 0)
                        .GroupBy(d => d.MaDichVu)
                        .Select(g => new
                        {
                            MaDichVu = g.Key,
                            TongSoLuong = g.Sum(x => x.SoLuong)
                        })
                        .ToList();

                    foreach (var itemKho in tongDichVuTheoKho)
                    {
                        var dichVu = db.DichVus
                            .FirstOrDefault(d =>
                                d.MaDichVu == itemKho.MaDichVu);

                        if (dichVu == null)
                        {
                            transaction.Rollback();
                            TempData["Loi"] = "Có dịch vụ không tồn tại.";
                            return RedirectToAction("Index");
                        }

                        if (itemKho.TongSoLuong > dichVu.SoLuongTon)
                        {
                            transaction.Rollback();
                            TempData["Loi"] =
                                "Dịch vụ \"" + dichVu.TenDichVu +
                                "\" chỉ còn " + dichVu.SoLuongTon +
                                " sản phẩm.";
                            return RedirectToAction("Index");
                        }
                    }

                    // =================================================
                    // TÍNH TỔNG TIỀN TRƯỚC GIẢM
                    // =================================================
                    decimal tongTienTruocGiam = 0m;

                    foreach (var item in dsChon)
                    {
                        var giaSan = db.GiaTheoKhungGios
                            .FirstOrDefault(g =>
                                g.MaSan == item.MaSan &&
                                g.MaKhungGio == item.MaKhungGio);

                        if (giaSan == null)
                        {
                            transaction.Rollback();
                            TempData["Loi"] = "Không xác định được giá sân.";
                            return RedirectToAction("Index");
                        }

                        decimal tongTienDichVuSan = 0m;

                        if (item.DichVus != null)
                        {
                            foreach (var dvChon in
                                item.DichVus.Where(x => x.SoLuong > 0))
                            {
                                var dichVu = db.DichVus
                                    .FirstOrDefault(d =>
                                        d.MaDichVu == dvChon.MaDichVu);

                                if (dichVu != null)
                                {
                                    tongTienDichVuSan +=
                                        dichVu.DonGia * dvChon.SoLuong;
                                }
                            }
                        }

                        tongTienTruocGiam +=
                            giaSan.DonGia + tongTienDichVuSan;
                    }

                    MaGiamGia maGiamGiaApDung = null;
                    decimal tienGiam = 0m;

                    if (!string.IsNullOrWhiteSpace(maCode))
                    {
                        string loiMa;

                        maGiamGiaApDung = KiemTraMaGiamGia(
                            maCode,
                            tongTienTruocGiam,
                            ngayDat,
                            out loiMa);

                        if (maGiamGiaApDung == null)
                        {
                            transaction.Rollback();
                            TempData["Loi"] = loiMa;
                            return RedirectToAction("Index");
                        }

                        tienGiam = TinhTienGiam(
                            maGiamGiaApDung,
                            tongTienTruocGiam);
                    }

                    // =================================================
                    // TẠO ĐƠN
                    // =================================================
                    List<DatSan> dsDonDaTao = new List<DatSan>();

                    for (int i = 0; i < dsChon.Count; i++)
                    {
                        var item = dsChon[i];

                        var giaSan = db.GiaTheoKhungGios
                            .First(g =>
                                g.MaSan == item.MaSan &&
                                g.MaKhungGio == item.MaKhungGio);

                        decimal tongTienDichVuSan = 0m;

                        if (item.DichVus != null)
                        {
                            foreach (var dvChon in
                                item.DichVus.Where(x => x.SoLuong > 0))
                            {
                                var dichVu = db.DichVus
                                    .FirstOrDefault(d =>
                                        d.MaDichVu == dvChon.MaDichVu);

                                if (dichVu != null)
                                {
                                    tongTienDichVuSan +=
                                        dichVu.DonGia * dvChon.SoLuong;
                                }
                            }
                        }

                        decimal tongTienDon =
                            giaSan.DonGia + tongTienDichVuSan;

                        decimal tienGiamChoDon = 0m;

                        if (i == 0)
                        {
                            tienGiamChoDon = tienGiam;

                            if (tienGiamChoDon > tongTienDon)
                                tienGiamChoDon = tongTienDon;
                        }

                        decimal tongTienCuoi =
                            tongTienDon - tienGiamChoDon;

                        if (tongTienCuoi < 0)
                            tongTienCuoi = 0;

                        var datSan = new DatSan
                        {
                            MaNguoiDung = maNguoiDungDatSan,
                            MaSan = item.MaSan,
                            MaKhungGio = item.MaKhungGio,
                            NgayDat = ngayDat,
                            TongTien = tongTienCuoi,

                            // Khách hàng tự đặt:
                            //   Chờ thanh toán trong 15 phút.
                            //
                            // Nhân viên đặt tại quầy:
                            //   Đơn được xác nhận ngay và không chạy hạn thanh toán 15 phút.
                            TrangThaiThanhToan =
                                vaiTro == "NhanVien"
                                    ? trangThaiThanhToanNhanVien
                                    : "Chưa thanh toán",

                            TrangThai =
                                vaiTro == "NhanVien"
                                    ? "Đã xác nhận"
                                    : "Chờ thanh toán",

                            HinhThucThanhToan =
                                vaiTro == "NhanVien"
                                    ? hinhThucThanhToanNhanVien
                                    : null,

                            HanThanhToan =
                                vaiTro == "NhanVien"
                                    ? (DateTime?)null
                                    : DateTime.Now.AddMinutes(15),

                            NgayThanhToan =
                                vaiTro == "NhanVien"
                                    ? ngayThanhToanNhanVien
                                    : null,

                            // Chỉ có giá trị khi Nhân viên đặt thay khách.
                            HoTenKhachHang =
                                vaiTro == "NhanVien"
                                    ? hoTenKhachHang
                                    : null,

                            SoDienThoaiKhachHang =
                                vaiTro == "NhanVien"
                                    ? soDienThoaiKhachHang
                                    : null,

                            EmailKhachHang =
                                vaiTro == "NhanVien" &&
                                !string.IsNullOrWhiteSpace(emailKhachHang)
                                    ? emailKhachHang
                                    : null
                        };

                        if (maGiamGiaApDung != null)
                        {
                            datSan.MaGiamGiaID =
                                maGiamGiaApDung.MaGiamGiaID;
                        }

                        db.DatSans.Add(datSan);
                        dsDonDaTao.Add(datSan);
                    }

                    db.SaveChanges();

                    // =================================================
                    // LƯU CHI TIẾT DỊCH VỤ
                    // =================================================
                    for (int i = 0; i < dsChon.Count; i++)
                    {
                        var itemSan = dsChon[i];
                        var datSan = dsDonDaTao[i];

                        if (itemSan.DichVus == null)
                            continue;

                        foreach (var dvChon in
                            itemSan.DichVus.Where(x => x.SoLuong > 0))
                        {
                            var dichVu = db.DichVus
                                .FirstOrDefault(d =>
                                    d.MaDichVu == dvChon.MaDichVu);

                            if (dichVu == null)
                                continue;

                            db.ChiTietDichVuDats.Add(
                                new ChiTietDichVuDat
                                {
                                    MaDatSan = datSan.MaDatSan,
                                    MaDichVu = dichVu.MaDichVu,
                                    SoLuong = dvChon.SoLuong,
                                    DonGiaTaiThoiDiemThat = dichVu.DonGia
                                });
                        }
                    }

                    foreach (var itemKho in tongDichVuTheoKho)
                    {
                        var dichVu = db.DichVus
                            .FirstOrDefault(d =>
                                d.MaDichVu == itemKho.MaDichVu);

                        if (dichVu != null)
                            dichVu.SoLuongTon -= itemKho.TongSoLuong;
                    }

                    // Mã giảm giá chỉ trừ 1 lượt cho toàn bộ giao dịch.
                    if (maGiamGiaApDung != null)
                    {
                        var maGiamGia = db.MaGiamGias
                            .FirstOrDefault(x =>
                                x.MaGiamGiaID ==
                                maGiamGiaApDung.MaGiamGiaID);

                        if (maGiamGia == null || maGiamGia.SoLuong <= 0)
                        {
                            transaction.Rollback();
                            TempData["Loi"] =
                                "Mã giảm giá vừa hết lượt sử dụng.";
                            return RedirectToAction("Index");
                        }

                        maGiamGia.SoLuong -= 1;
                    }

                    // =================================================
                    // THÔNG BÁO
                    // =================================================
                    if (dsDonDaTao.Any())
                    {
                        string noiDungThongBao;

                        // Nhân viên tạo đơn thay khách:
                        // nếu khách có tài khoản thì gửi thông báo cho khách.
                        // Khách vãng lai (MaNguoiDung = NULL) sẽ tự bỏ qua
                        // trong hàm TaoThongBao(...).
                        if (vaiTro == "NhanVien")
                        {
                            string danhSachMaDonNhanVien =
                                string.Join(
                                    ", ",
                                    dsDonDaTao.Select(d =>
                                        "#" + d.MaDatSan));

                            string moTaThanhToan =
                                "Đã thanh toán bằng " +
                                hinhThucThanhToanNhanVien + ".";

                            noiDungThongBao =
                                "Nhân viên đã tạo đơn đặt sân " +
                                danhSachMaDonNhanVien +
                                " cho ngày " +
                                ngayDat.ToString("dd/MM/yyyy") +
                                ". " + moTaThanhToan;

                            TaoThongBao(
                                maNguoiDungDatSan,
                                "Nhân viên đã đặt sân cho bạn",
                                noiDungThongBao,
                                Url.Action("TrangThai", "DatSan"));
                        }
                        else
                        {
                            // Khách hàng tự đặt: giữ nguyên luồng cũ.
                            if (dsDonDaTao.Count == 1)
                            {
                                var don = dsDonDaTao[0];
                                var kg = db.KhungGios.Find(don.MaKhungGio);

                                noiDungThongBao =
                                    "Đơn đặt sân #" + don.MaDatSan +
                                    " ngày " + ngayDat.ToString("dd/MM/yyyy") +
                                    (kg != null
                                        ? " lúc " +
                                          kg.GioBatDau.ToString(@"hh\:mm") +
                                          " - " +
                                          kg.GioKetThuc.ToString(@"hh\:mm")
                                        : "") +
                                    " đã được tạo. Vui lòng hoàn tất thanh toán trong 15 phút.";
                            }
                            else
                            {
                                string danhSachMaDon = string.Join(
                                    ", ",
                                    dsDonDaTao.Select(d =>
                                        "#" + d.MaDatSan));

                                noiDungThongBao =
                                    "Bạn đã tạo " +
                                    dsDonDaTao.Count +
                                    " đơn đặt sân (" +
                                    danhSachMaDon +
                                    ") cho ngày " +
                                    ngayDat.ToString("dd/MM/yyyy") +
                                    ". Vui lòng hoàn tất thanh toán trong 15 phút.";
                            }

                            TaoThongBao(
                                maNguoiDungDatSan,
                                "Đơn đặt sân đã được tạo",
                                noiDungThongBao,
                                Url.Action("TrangThai", "DatSan"));
                        }
                    }

                    db.SaveChanges();
                    transaction.Commit();

                    // Nhân viên tạo đơn trực tiếp tại quầy:
                    // không đưa qua màn hình QR/thanh toán của Khách hàng.
                    if (vaiTro == "NhanVien")
                    {
                        string danhSachMaDon =
                            string.Join(
                                ", ",
                                dsDonDaTao.Select(d =>
                                    "#" + d.MaDatSan));

                        string thongTinThanhToan =
                            "Đã thanh toán bằng " +
                            hinhThucThanhToanNhanVien + ".";

                        TempData["ThanhCong"] =
                            "Đã tạo đơn " +
                            danhSachMaDon +
                            " cho khách " +
                            hoTenKhachHang +
                            ". " +
                            thongTinThanhToan;

                        return RedirectToAction("TrangThai");
                    }

                    // Khách hàng tự đặt: giữ nguyên luồng thanh toán cũ.
                    if (dsDonDaTao.Count == 1)
                    {
                        return RedirectToAction(
                            "ThanhToan",
                            new { id = dsDonDaTao[0].MaDatSan });
                    }

                    Session["DanhSachMaDatSanGop"] =
                        dsDonDaTao
                            .Select(d => d.MaDatSan)
                            .ToList();

                    return RedirectToAction("ThanhToanGop");
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

                    TempData["Loi"] =
                        "Hệ thống đang xử lý một yêu cầu khác. Vui lòng thử lại.";

                    return RedirectToAction("Index");
                }
            }
        }

        // =========================================================
        // THANH TOÁN ĐƠN LẺ
        // =========================================================
        public ActionResult ThanhToan(int id)
        {
            if (!DaDangNhap())
            {
                return RedirectToAction(
                    "DangNhap",
                    "TaiKhoan");
            }

            // Nhân viên tạo/thu tiền trực tiếp tại quầy,
            // không sử dụng màn hình QR của Khách hàng.
            if (LayVaiTro() == "NhanVien")
            {
                TempData["Loi"] =
                    "Nhân viên không sử dụng màn hình thanh toán của Khách hàng.";

                return RedirectToAction("TrangThai");
            }

            HuyDonHetHan();

            var datSan =
                db.DatSans
                .Include(d => d.SanBong)
                .Include(d => d.KhungGio)
                .Include(d => d.NguoiDung)
                .Include(d =>
                    d.ChiTietDichVuDats
                        .Select(c => c.DichVu))
                .Include(d => d.MaGiamGia)
                .FirstOrDefault(d =>
                    d.MaDatSan == id);

            if (datSan == null)
            {
                return HttpNotFound();
            }

            if (!CoQuyenXemDon(datSan))
            {
                return new HttpStatusCodeResult(403);
            }

            if (datSan.TrangThai == "Đã hủy")
            {
                TempData["Loi"] =
                    "Đơn đặt sân đã bị hủy.";

                return RedirectToAction(
                    "TrangThai");
            }

            if (datSan.TrangThai == "Yêu cầu hủy")
            {
                TempData["Loi"] =
                    "Đơn đang chờ Nhân viên xử lý yêu cầu hủy nên không thể tiếp tục thanh toán.";

                return RedirectToAction("TrangThai");
            }

            var danhSachThanhToan =
                new List<DatSan>
                {
                    datSan
                };

            return View(
                "ThanhToanGop",
                danhSachThanhToan);
        }

        // =========================================================
        // THANH TOÁN GỘP
        // =========================================================
        public ActionResult ThanhToanGop()
        {
            if (!DaDangNhap())
            {
                return RedirectToAction(
                    "DangNhap",
                    "TaiKhoan");
            }

            // Nhân viên không đi qua luồng QR/thanh toán gộp của Khách hàng.
            if (LayVaiTro() == "NhanVien")
            {
                return RedirectToAction("TrangThai");
            }

            HuyDonHetHan();

            var dsMaDat =
                Session[
                    "DanhSachMaDatSanGop"]
                as List<int>;

            if (
                dsMaDat == null ||
                !dsMaDat.Any())
            {
                return RedirectToAction(
                    "TrangThai");
            }

            int? maNguoiDung =
                LayMaNguoiDung();

            if (!maNguoiDung.HasValue)
            {
                return RedirectToAction(
                    "DangNhap",
                    "TaiKhoan");
            }

            var dsDatSan =
                db.DatSans
                .Include(d => d.SanBong)
                .Include(d => d.KhungGio)
                .Include(d => d.NguoiDung)
                .Include(d =>
                    d.ChiTietDichVuDats
                        .Select(c => c.DichVu))
                .Include(d => d.MaGiamGia)
                .Where(d =>
                    dsMaDat.Contains(
                        d.MaDatSan) &&
                    d.MaNguoiDung ==
                        maNguoiDung.Value &&
                    d.TrangThai != "Đã hủy" &&
                    d.TrangThai != "Yêu cầu hủy")
                .OrderBy(d => d.MaDatSan)
                .ToList();

            if (!dsDatSan.Any())
            {
                return RedirectToAction(
                    "TrangThai");
            }

            return View(dsDatSan);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult XacNhanChuyenKhoan(string danhSachIdGop)
        {
            if (!DaDangNhap())
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            if (string.IsNullOrWhiteSpace(danhSachIdGop))
            {
                TempData["Loi"] = "Không có thông tin đặt sân nào.";
                return RedirectToAction("TrangThai");
            }

            int? maNguoiDung = LayMaNguoiDung();

            if (!maNguoiDung.HasValue)
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            var listId = danhSachIdGop
                .Split(',')
                .Select(s =>
                {
                    int id;
                    return int.TryParse(s, out id) ? id : 0;
                })
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (!listId.Any())
            {
                TempData["Loi"] = "Danh sách đơn đặt sân không hợp lệ.";
                return RedirectToAction("TrangThai");
            }

            var danhSachDon = db.DatSans
                .Where(d =>
                    listId.Contains(d.MaDatSan) &&
                    d.MaNguoiDung == maNguoiDung.Value)
                .ToList();

            if (!danhSachDon.Any())
            {
                TempData["Loi"] = "Không tìm thấy đơn đặt sân.";
                return RedirectToAction("TrangThai");
            }

            var danhSachDonHopLe = danhSachDon
                .Where(d =>
                    d.TrangThai != "Đã hủy" &&
                    d.TrangThai != "Yêu cầu hủy" &&
                    d.TrangThai != "Hoàn thành")
                .ToList();

            if (!danhSachDonHopLe.Any())
            {
                TempData["Loi"] =
                    "Các đơn đã bị hủy hoặc không còn hợp lệ.";

                return RedirectToAction("TrangThai");
            }

            foreach (var datSan in danhSachDonHopLe)
            {
                // Khách thông báo đã chuyển khoản
                datSan.HinhThucThanhToan = "Chuyển khoản";

                // Chưa xem là đã thanh toán cho tới khi quản lý/nhân viên xác nhận
                datSan.TrangThaiThanhToan = "Chờ xác nhận";

                // Đơn đang chờ quản lý/nhân viên duyệt
                datSan.TrangThai = "Chờ xác nhận";
            }

            // =====================================================
            // THÔNG BÁO CHO KHÁCH: ĐANG CHỜ XÁC NHẬN
            // =====================================================
            string danhSachMaDonChoXacNhan =
                string.Join(
                    ", ",
                    danhSachDonHopLe.Select(d =>
                        "#" + d.MaDatSan)
                );

            TaoThongBao(
                maNguoiDung.Value,
                "Đã gửi xác nhận thanh toán",
                "Hệ thống đã ghi nhận thanh toán cho đơn " +
                danhSachMaDonChoXacNhan +
                ". Vui lòng chờ Nhân viên xác nhận.",
                Url.Action("TrangThai", "DatSan")
            );

            db.SaveChanges();

            Session.Remove("DanhSachMaDatSanGop");

            TempData["ThanhCong"] =
                "Đã ghi nhận thanh toán. Vui lòng chờ Nhân viên xác nhận.";

            return RedirectToAction("TrangThai");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult XacNhanThanhToan(int id)
        {
            if (!DaDangNhap())
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            string vaiTro = LayVaiTro();

            if (vaiTro != "NhanVien")
            {
                return new HttpStatusCodeResult(403);
            }

            var datSan = db.DatSans.Find(id);

            if (datSan == null)
            {
                return HttpNotFound();
            }

            if (datSan.TrangThai == "Yêu cầu hủy")
            {
                TempData["Loi"] =
                    "Đơn này đang có yêu cầu hủy. Vui lòng xử lý yêu cầu hủy trước.";

                return RedirectToAction("TrangThai");
            }

            if (datSan.TrangThaiThanhToan != "Chờ xác nhận")
            {
                TempData["Loi"] = "Đơn này không ở trạng thái chờ xác nhận thanh toán.";
                return RedirectToAction("TrangThai");
            }

            datSan.TrangThaiThanhToan = "Đã thanh toán";
            datSan.TrangThai = "Đã xác nhận";
            datSan.NgayThanhToan = DateTime.Now;
            datSan.HanThanhToan = null;

            // =====================================================
            // THÔNG BÁO CHO KHÁCH: ĐƠN ĐÃ ĐƯỢC XÁC NHẬN
            // =====================================================
            TaoThongBao(
                datSan.MaNguoiDung,
                "Đặt sân đã được xác nhận",
                "Đơn đặt sân #" +
                datSan.MaDatSan +
                " đã được Nhân viên xác nhận thành công. " +
                "Bạn có thể xem chi tiết đơn trong Lịch Sử Đặt Sân.",
                Url.Action(
                    "ChiTiet",
                    "DatSan",
                    new
                    {
                        id = datSan.MaDatSan
                    }
                )
            );

            db.SaveChanges();

            TempData["ThanhCong"] =
                "Đã xác nhận thanh toán và gửi thông báo cho khách hàng.";

            return RedirectToAction("TrangThai");
        }

        // =========================================================
        // XÁC NHẬN THANH TOÁN TRỰC TIẾP (Nhiều sân)
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult XacNhanTrucTiep(string danhSachIdGop)
        {
            if (string.IsNullOrEmpty(danhSachIdGop))
            {
                TempData["Loi"] = "Không có thông tin đặt sân nào.";
                return RedirectToAction("Index");
            }

            var listId = danhSachIdGop.Split(',')
                                      .Select(s => int.TryParse(s, out int id) ? id : 0)
                                      .Where(id => id > 0)
                                      .ToList();

            foreach (var id in listId)
            {
                var datSan = db.DatSans.Find(id);
                if (datSan != null &&
                    CoQuyenXemDon(datSan) &&
                    datSan.TrangThai != "Đã hủy" &&
                    datSan.TrangThai != "Yêu cầu hủy")
                {
                    datSan.TrangThai = "Chờ xác nhận";
                    datSan.TrangThaiThanhToan = "Thanh toán trực tiếp tại sân";
                }
            }

            db.SaveChanges();
            TempData["ThongBao"] = "Đã đăng ký phương thức thanh toán trực tiếp tại sân!";
            return RedirectToAction("DatSanThanhCong");
        }
        // =========================================================
        // TRANG THÔNG BÁO ĐẶT SÂN THÀNH CÔNG
        // =========================================================
        [HttpGet]
        public ActionResult DatSanThanhCong()
        {
            return View();
        }





        // =========================================================
        // GIẢI PHÓNG DATABASE
        // =========================================================
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }

            base.Dispose(disposing);
        }


        // =========================================================
        // XEM CHI TIẾT ĐƠN ĐẶT SÂN
        // =========================================================
        [HttpGet]
        public ActionResult ChiTiet(int? id)
        {
            if (!DaDangNhap())
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            // Dùng .Include để lấy đầy đủ các bảng liên quan tránh bị lỗi null ở View
            var datSan = db.DatSans
                .Include(d => d.SanBong)
                .Include(d => d.KhungGio)
                .Include(d => d.NguoiDung)
                .Include(d => d.MaGiamGia)
                .Include(d => d.ChiTietDichVuDats.Select(c => c.DichVu))
                .FirstOrDefault(d => d.MaDatSan == id.Value);

            if (datSan == null)
            {
                return HttpNotFound();
            }

            // Kiểm tra quyền xem đơn (chỉ Admin, Nhân viên hoặc chính chủ đơn hàng mới được xem)
            if (!CoQuyenXemDon(datSan))
            {
                TempData["Loi"] = "Bạn không có quyền xem đơn đặt sân này.";
                return RedirectToAction("TrangThai");
            }

            return View(datSan);
        }
    }
}