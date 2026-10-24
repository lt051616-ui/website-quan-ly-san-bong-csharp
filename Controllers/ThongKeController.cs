using Newtonsoft.Json;
using Phân_Tích_Thiết_Kế_Website_QL_Sân_Bóng.Models;
using System;
using System.Linq;
using System.Web.Mvc;

namespace Phan_Tich_Thiet_Ke_Website_QL_San_Bong.Controllers
{
    public class ThongKeController : Controller
    {
        private QLSanBongEntities db = new QLSanBongEntities();

        public ActionResult Index(DateTime? tuNgay, DateTime? denNgay)
        {
            if (Session["VaiTro"] == null || Session["VaiTro"].ToString().Trim() != "Admin")
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            DateTime start = tuNgay ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            DateTime end = denNgay ?? DateTime.Now.Date.AddDays(1).AddTicks(-1);

            ViewBag.TuNgay = start.ToString("yyyy-MM-dd");
            ViewBag.DenNgay = end.ToString("yyyy-MM-dd");

            // 1. Lấy danh sách lượt đặt sân trong khoảng thời gian
            var dsDatSan = db.DatSans
                .Where(d => d.NgayDat >= start && d.NgayDat <= end)
                .ToList();

            // 2. Thống kê số liệu
            // Thay 'TongTien' bằng tên cột tiền trong bảng DatSan của bạn nếu bị gạch đỏ (ví dụ: TienSan, Gia)
            ViewBag.TongDoanhThu = dsDatSan.Sum(d => (decimal?)(d.TongTien)) ?? 0;
            ViewBag.TongLuotDat = dsDatSan.Count;

            // Đếm khách hàng (Nếu DbContext đặt tên là KhachHang thay vì KhachHangs)
            ViewBag.KhachHangMoi = db.NguoiDungs.Count();

            // Tính tỷ lệ lấp đầy (Sử dụng db.SanBongs thay vì db.Sans)
            int tongSoSan = db.SanBongs.Count();
            int soNgay = (end - start).Days + 1;
            int khungGioMoiNgay = 10;
            int tongCaToiDa = tongSoSan * soNgay * khungGioMoiNgay;

            ViewBag.TyLeLapDien = tongCaToiDa > 0
                ? (int)Math.Round((double)dsDatSan.Count / tongCaToiDa * 100)
                : 0;

            // 3. Biểu đồ doanh thu 12 tháng năm hiện tại (NgayDat không null nên bỏ .HasValue)
            int currentYear = DateTime.Now.Year;
            var doanhThuTheoThang = db.DatSans
                .Where(d => d.NgayDat.Year == currentYear)
                .GroupBy(d => d.NgayDat.Month)
                .Select(g => new { Thang = g.Key, DoanhThu = g.Sum(x => (decimal?)x.TongTien) ?? 0 })
                .ToList();

            decimal[] dataChartThang = new decimal[12];
            foreach (var item in doanhThuTheoThang)
            {
                dataChartThang[item.Thang - 1] = item.DoanhThu;
            }
            ViewBag.DataDoanhThuThang = JsonConvert.SerializeObject(dataChartThang);

            // 4. Biểu đồ loại sân (Sử dụng d.SanBong thay vì d.San)
            var tyLeLoaiSan = db.DatSans
                .Where(d => d.NgayDat >= start && d.NgayDat <= end && d.SanBong != null && d.SanBong.LoaiSan != null)
                .GroupBy(d => d.SanBong.LoaiSan)
                .Select(g => new { TenLoai = g.Key, SoLuong = g.Count() })
                .ToList();

            ViewBag.LabelsLoaiSan = JsonConvert.SerializeObject(tyLeLoaiSan.Select(x => x.TenLoai));
            ViewBag.DataLoaiSan = JsonConvert.SerializeObject(tyLeLoaiSan.Select(x => x.SoLuong));

            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}   