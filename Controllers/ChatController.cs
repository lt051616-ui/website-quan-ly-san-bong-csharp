using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Newtonsoft.Json.Linq;
using Phân_Tích_Thiết_Kế_Website_QL_Sân_Bóng.Models;

namespace Phân_Tích_Thiết_Kế_Website_QL_Sân_Bóng.Controllers
{
    public class ChatController : Controller
    {
        private QLSanBongEntities db = new QLSanBongEntities();

        public class CuocTroChuyenNhanVienVM
        {
            public int MaCuocTroChuyen { get; set; }
            public string TenKhachHang { get; set; }
            public string TrangThai { get; set; }
            public string TinNhanCuoi { get; set; }
            public int SoTinChuaDoc { get; set; }
            public DateTime NgayCapNhat { get; set; }
        }

        private bool DaDangNhap()
        {
            return Session["MaNguoiDung"] != null;
        }

        private int? LayMaNguoiDung()
        {
            if (Session["MaNguoiDung"] == null) return null;
            int id;
            return int.TryParse(Session["MaNguoiDung"].ToString(), out id)
                ? (int?)id
                : null;
        }

        private string LayVaiTro()
        {
            return Session["VaiTro"] == null ? "" : Session["VaiTro"].ToString();
        }

        private void TaoThongBao(int maNguoiDung, string tieuDe, string noiDung, string duongDan = null)
        {
            db.ThongBaos.Add(new ThongBao
            {
                MaNguoiDung = maNguoiDung,
                TieuDe = tieuDe,
                NoiDung = noiDung,
                DuongDan = duongDan,
                DaDoc = false,
                NgayTao = DateTime.Now
            });
        }

        private void GuiThongBaoChoNhanVien(string tieuDe, string noiDung, string duongDan = null)
        {
            var dsNhanVien = db.NguoiDungs
                .Where(x => x.VaiTro == "NhanVien")
                .ToList();

            foreach (var nv in dsNhanVien)
            {
                TaoThongBao(nv.MaNguoiDung, tieuDe, noiDung, duongDan);
            }
        }

        private CuocTroChuyen LayHoacTaoCuocTroChuyen(int maKhachHang)
        {
            var cuoc = db.CuocTroChuyens
                .Where(x => x.MaKhachHang == maKhachHang && x.TrangThai != "Đã đóng")
                .OrderByDescending(x => x.NgayCapNhat)
                .FirstOrDefault();

            if (cuoc != null) return cuoc;

            cuoc = new CuocTroChuyen
            {
                MaKhachHang = maKhachHang,
                MaNhanVienPhuTrach = null,
                TrangThai = "AI đang hỗ trợ",
                NgayTao = DateTime.Now,
                NgayCapNhat = DateTime.Now
            };

            db.CuocTroChuyens.Add(cuoc);
            db.SaveChanges();

            db.TinNhans.Add(new TinNhan
            {
                MaCuocTroChuyen = cuoc.MaCuocTroChuyen,
                MaNguoiGui = null,
                LoaiNguoiGui = "AI",
                NoiDung = "Xin chào! Tôi là trợ lý hỗ trợ đặt sân. Bạn có thể hỏi giá sân, chính sách hủy hoặc bấm Gặp Nhân viên.",
                DaDoc = false,
                ThoiGianGui = DateTime.Now
            });

            db.SaveChanges();
            return cuoc;
        }

        // Tích hợp Gemini API - Đọc model linh hoạt từ Web.config
        private async Task<Tuple<string, bool>> TaoTraLoiTuDongAsync(string noiDung)
        {
            bool canNhanVien = false;
            string q = (noiDung ?? "").Trim().ToLower();

            // Kiểm tra từ khóa yêu cầu chuyển người thật
            if (q.Contains("nhân viên") || q.Contains("nhan vien") || q.Contains("chủ sân") || q.Contains("chu san") || q.Contains("người thật") || q.Contains("nguoi that"))
            {
                canNhanVien = true;
                return new Tuple<string, bool>("Mình đã chuyển cuộc trò chuyện đến Nhân viên. Bạn vui lòng chờ một chút nhé.", canNhanVien);
            }

            string apiKey = ConfigurationManager.AppSettings["GeminiApiKey"];
            string modelName = ConfigurationManager.AppSettings["GeminiModel"] ?? "gemini-1.5-flash";

            if (string.IsNullOrEmpty(apiKey))
            {
                return new Tuple<string, bool>("Hệ thống AI chưa được cấu hình API Key. Vui lòng liên hệ quản trị viên.", false);
            }

            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";

            // FIX: Lấy dữ liệu thô về bộ nhớ RAM trước rồi mới định dạng chuỗi để tránh lỗi ELinq của Entity Framework
            var dsSanRaw = db.SanBongs
                .Where(x => x.TrangThai == "Hoạt động" && x.DonGiaTheoGio != null)
                .Select(x => new { x.TenSan, x.DonGiaTheoGio })
                .ToList();

            var dsSan = dsSanRaw
                .Select(x => x.TenSan + " (" + (x.DonGiaTheoGio ?? 0m).ToString("N0") + " VNĐ/giờ)")
                .ToList();

            string thongTinSanDb = dsSan.Any() ? string.Join(", ", dsSan) : "Chưa cập nhật giá sân";

            string systemInstruction = "Bạn là 'Sân Bóng AI' - Trợ lý chăm sóc khách hàng ảo thông minh của hệ thống Quản lý Sân Bóng đá.\n" +
                                       "- Dữ liệu sân hiện tại: " + thongTinSanDb + ".\n" +
                                       "- Chính sách hủy: Đơn chưa thanh toán được hủy miễn phí khi trước ít nhất 2 giờ; đơn đã thanh toán cần nhân viên xử lý.\n" +
                                       "Hãy trả lời ngắn gọn, lịch sự, thân thiện bằng tiếng Việt dựa trên dữ liệu trên.";

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = systemInstruction + "\n\nKhách hàng hỏi: " + noiDung }
                        }
                    }
                }
            };

            string jsonPayload = Newtonsoft.Json.JsonConvert.SerializeObject(payload);

            using (var client = new HttpClient())
            {
                try
                {
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(url, content);

                    if (response.IsSuccessStatusCode)
                    {
                        string jsonResponse = await response.Content.ReadAsStringAsync();
                        JObject data = JObject.Parse(jsonResponse);
                        string reply = data["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

                        if (!string.IsNullOrEmpty(reply))
                        {
                            return new Tuple<string, bool>(reply.Trim(), false);
                        }
                    }
                }
                catch (Exception)
                {
                    // Lỗi kết nối mạng hoặc gọi API
                }
            }

            canNhanVien = true;
            return new Tuple<string, bool>("Mình chưa thể kết nối tới hệ thống AI lúc này. Mình đã chuyển câu hỏi của bạn đến Nhân viên để hỗ trợ trực tiếp.", canNhanVien);
        }

        [HttpGet]
        public PartialViewResult KhachHang()
        {
            return PartialView(
                "~/Views/Chat/_ChatKhachHang.cshtml"
            );
        }

        [HttpGet]
        public JsonResult LayTinNhanKhachHang()
        {
            if (!DaDangNhap() || LayVaiTro() != "KhachHang")
                return Json(new { success = false, message = "Bạn chưa đăng nhập." }, JsonRequestBehavior.AllowGet);

            int maKhachHang = LayMaNguoiDung().Value;
            var cuoc = LayHoacTaoCuocTroChuyen(maKhachHang);

            var tinNhans = db.TinNhans
                .Where(x => x.MaCuocTroChuyen == cuoc.MaCuocTroChuyen)
                .OrderBy(x => x.ThoiGianGui)
                .ToList()
                .Select(x => new
                {
                    maTinNhan = x.MaTinNhan,
                    loaiNguoiGui = x.LoaiNguoiGui,
                    noiDung = x.NoiDung,
                    thoiGianGui = x.ThoiGianGui.ToString("HH:mm dd/MM/yyyy")
                })
                .ToList();

            var chuaDoc = db.TinNhans
                .Where(x => x.MaCuocTroChuyen == cuoc.MaCuocTroChuyen &&
                            x.LoaiNguoiGui != "KhachHang" &&
                            x.DaDoc == false)
                .ToList();

            foreach (var tin in chuaDoc) tin.DaDoc = true;
            db.SaveChanges();

            return Json(new
            {
                success = true,
                maCuocTroChuyen = cuoc.MaCuocTroChuyen,
                trangThai = cuoc.TrangThai,
                tinNhans = tinNhans
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> GuiTinNhanKhachHang(string noiDung)
        {
            if (!DaDangNhap() || LayVaiTro() != "KhachHang")
                return Json(new { success = false, message = "Bạn không có quyền gửi tin nhắn." });

            if (string.IsNullOrWhiteSpace(noiDung))
                return Json(new { success = false, message = "Vui lòng nhập nội dung tin nhắn." });

            noiDung = noiDung.Trim();
            if (noiDung.Length > 2000) noiDung = noiDung.Substring(0, 2000);

            int maKhachHang = LayMaNguoiDung().Value;
            var cuoc = LayHoacTaoCuocTroChuyen(maKhachHang);

            db.TinNhans.Add(new TinNhan
            {
                MaCuocTroChuyen = cuoc.MaCuocTroChuyen,
                MaNguoiGui = maKhachHang,
                LoaiNguoiGui = "KhachHang",
                NoiDung = noiDung,
                DaDoc = false,
                ThoiGianGui = DateTime.Now
            });

            cuoc.NgayCapNhat = DateTime.Now;

            // LUÔN ƯU TIÊN GỌI AI TRẢ LỚI TRƯỚC CHO MỌI TIN NHẮN
            var aiResult = await TaoTraLoiTuDongAsync(noiDung);
            string traLoi = aiResult.Item1;
            bool canNhanVien = aiResult.Item2;

            if (!string.IsNullOrWhiteSpace(traLoi))
            {
                db.TinNhans.Add(new TinNhan
                {
                    MaCuocTroChuyen = cuoc.MaCuocTroChuyen,
                    MaNguoiGui = null,
                    LoaiNguoiGui = "AI",
                    NoiDung = traLoi,
                    DaDoc = false,
                    ThoiGianGui = DateTime.Now.AddMilliseconds(1)
                });
            }

            // Nếu cuộc trò chuyện đang có nhân viên phụ trách, gửi thông báo nhắc nhở nhân viên
            if (cuoc.MaNhanVienPhuTrach.HasValue)
            {
                TaoThongBao(
                    cuoc.MaNhanVienPhuTrach.Value,
                    "Khách hàng vừa gửi tin nhắn",
                    "Có tin nhắn mới trong cuộc trò chuyện #" + cuoc.MaCuocTroChuyen + ".",
                    Url.Action("NhanVien", "Chat", new { id = cuoc.MaCuocTroChuyen })
                );
            }

            if (canNhanVien)
            {
                cuoc.TrangThai = "Chờ nhân viên";
                GuiThongBaoChoNhanVien(
                    "Có khách hàng cần hỗ trợ",
                    "Cuộc trò chuyện #" + cuoc.MaCuocTroChuyen + " đang chờ Nhân viên phản hồi.",
                    Url.Action("NhanVien", "Chat", new { id = cuoc.MaCuocTroChuyen })
                );
            }

            db.SaveChanges();
            return Json(new { success = true, daChuyenNhanVien = canNhanVien });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult ChuyenNhanVien()
        {
            if (!DaDangNhap() || LayVaiTro() != "KhachHang")
                return Json(new { success = false });

            int maKhachHang = LayMaNguoiDung().Value;
            var cuoc = LayHoacTaoCuocTroChuyen(maKhachHang);

            if (cuoc.TrangThai != "Nhân viên đang hỗ trợ")
                cuoc.TrangThai = "Chờ nhân viên";

            cuoc.NgayCapNhat = DateTime.Now;

            db.TinNhans.Add(new TinNhan
            {
                MaCuocTroChuyen = cuoc.MaCuocTroChuyen,
                MaNguoiGui = null,
                LoaiNguoiGui = "AI",
                NoiDung = "Mình đã chuyển cuộc trò chuyện đến Nhân viên. Bạn vui lòng chờ một chút nhé.",
                DaDoc = false,
                ThoiGianGui = DateTime.Now
            });

            GuiThongBaoChoNhanVien(
                "Có khách hàng yêu cầu hỗ trợ",
                "Cuộc trò chuyện #" + cuoc.MaCuocTroChuyen + " đang chờ Nhân viên tiếp nhận.",
                Url.Action("NhanVien", "Chat", new { id = cuoc.MaCuocTroChuyen })
            );

            db.SaveChanges();
            return Json(new { success = true });
        }

        [HttpGet]
        public ActionResult NhanVien(int? id)
        {
            if (!DaDangNhap())
                return RedirectToAction("DangNhapNoiBo", "TaiKhoan");

            if (LayVaiTro() != "NhanVien")
                return new HttpStatusCodeResult(403);

            int maNhanVien = LayMaNguoiDung().Value;

            var dsCuoc = db.CuocTroChuyens
                .Where(x => x.TrangThai != "Đã đóng" &&
                            (x.MaNhanVienPhuTrach == null || x.MaNhanVienPhuTrach == maNhanVien))
                .OrderBy(x => x.TrangThai == "Chờ nhân viên" ? 0 : 1)
                .ThenByDescending(x => x.NgayCapNhat)
                .ToList();

            var model = new List<CuocTroChuyenNhanVienVM>();

            foreach (var cuoc in dsCuoc)
            {
                var khach = db.NguoiDungs.FirstOrDefault(x => x.MaNguoiDung == cuoc.MaKhachHang);
                var tinCuoi = db.TinNhans
                    .Where(x => x.MaCuocTroChuyen == cuoc.MaCuocTroChuyen)
                    .OrderByDescending(x => x.ThoiGianGui)
                    .FirstOrDefault();

                int soTinChuaDoc = db.TinNhans.Count(x =>
                    x.MaCuocTroChuyen == cuoc.MaCuocTroChuyen &&
                    x.LoaiNguoiGui == "KhachHang" &&
                    x.DaDoc == false);

                model.Add(new CuocTroChuyenNhanVienVM
                {
                    MaCuocTroChuyen = cuoc.MaCuocTroChuyen,
                    TenKhachHang = khach != null ? khach.HoTen : "Khách hàng",
                    TrangThai = cuoc.TrangThai,
                    TinNhanCuoi = tinCuoi != null ? tinCuoi.NoiDung : "",
                    SoTinChuaDoc = soTinChuaDoc,
                    NgayCapNhat = cuoc.NgayCapNhat
                });
            }

            ViewBag.MaCuocTroChuyenDangChon = id;
            return View(model);
        }

        [HttpGet]
        public JsonResult LayTinNhanNhanVien(int id)
        {
            if (!DaDangNhap() || LayVaiTro() != "NhanVien")
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            int maNhanVien = LayMaNguoiDung().Value;
            var cuoc = db.CuocTroChuyens.FirstOrDefault(x => x.MaCuocTroChuyen == id);

            if (cuoc == null)
                return Json(new { success = false, message = "Không tìm thấy cuộc trò chuyện." }, JsonRequestBehavior.AllowGet);

            if (cuoc.MaNhanVienPhuTrach.HasValue && cuoc.MaNhanVienPhuTrach.Value != maNhanVien)
                return Json(new { success = false, message = "Cuộc trò chuyện đang do Nhân viên khác phụ trách." }, JsonRequestBehavior.AllowGet);

            var khach = db.NguoiDungs.FirstOrDefault(x => x.MaNguoiDung == cuoc.MaKhachHang);

            var tinNhans = db.TinNhans
                .Where(x => x.MaCuocTroChuyen == id)
                .OrderBy(x => x.ThoiGianGui)
                .ToList()
                .Select(x => new
                {
                    loaiNguoiGui = x.LoaiNguoiGui,
                    noiDung = x.NoiDung,
                    thoiGianGui = x.ThoiGianGui.ToString("HH:mm dd/MM/yyyy")
                })
                .ToList();

            var chuaDoc = db.TinNhans
                .Where(x => x.MaCuocTroChuyen == id && x.LoaiNguoiGui == "KhachHang" && x.DaDoc == false)
                .ToList();

            foreach (var tin in chuaDoc) tin.DaDoc = true;
            db.SaveChanges();

            return Json(new
            {
                success = true,
                tenKhachHang = khach != null ? khach.HoTen : "Khách hàng",
                trangThai = cuoc.TrangThai,
                maNhanVienPhuTrach = cuoc.MaNhanVienPhuTrach,
                tinNhans = tinNhans
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult NhanCuocTroChuyen(int id)
        {
            if (!DaDangNhap() || LayVaiTro() != "NhanVien")
                return Json(new { success = false });

            int maNhanVien = LayMaNguoiDung().Value;
            var cuoc = db.CuocTroChuyens.FirstOrDefault(x => x.MaCuocTroChuyen == id);

            if (cuoc == null)
                return Json(new { success = false, message = "Không tìm thấy cuộc trò chuyện." });

            if (cuoc.MaNhanVienPhuTrach.HasValue && cuoc.MaNhanVienPhuTrach.Value != maNhanVien)
                return Json(new { success = false, message = "Cuộc trò chuyện đã được Nhân viên khác tiếp nhận." });

            cuoc.MaNhanVienPhuTrach = maNhanVien;
            cuoc.TrangThai = "Nhân viên đang hỗ trợ";
            cuoc.NgayCapNhat = DateTime.Now;

            var nv = db.NguoiDungs.FirstOrDefault(x => x.MaNguoiDung == maNhanVien);
            string tenNhanVien = nv != null ? nv.HoTen : "Nhân viên";

            db.TinNhans.Add(new TinNhan
            {
                MaCuocTroChuyen = cuoc.MaCuocTroChuyen,
                MaNguoiGui = maNhanVien,
                LoaiNguoiGui = "NhanVien",
                NoiDung = tenNhanVien + " đã tiếp nhận cuộc trò chuyện và đang hỗ trợ bạn.",
                DaDoc = false,
                ThoiGianGui = DateTime.Now
            });

            TaoThongBao(
                cuoc.MaKhachHang,
                "Nhân viên đã tiếp nhận hỗ trợ",
                tenNhanVien + " đã tiếp nhận cuộc trò chuyện của bạn.",
                Url.Action("Index", "DatSan")
            );

            db.SaveChanges();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult NhanVienTraLoi(int id, string noiDung)
        {
            if (!DaDangNhap() || LayVaiTro() != "NhanVien")
                return Json(new { success = false });

            if (string.IsNullOrWhiteSpace(noiDung))
                return Json(new { success = false, message = "Vui lòng nhập nội dung phản hồi." });

            noiDung = noiDung.Trim();
            if (noiDung.Length > 2000) noiDung = noiDung.Substring(0, 2000);

            int maNhanVien = LayMaNguoiDung().Value;
            var cuoc = db.CuocTroChuyens.FirstOrDefault(x => x.MaCuocTroChuyen == id);

            if (cuoc == null)
                return Json(new { success = false, message = "Không tìm thấy cuộc trò chuyện." });

            if (cuoc.MaNhanVienPhuTrach.HasValue && cuoc.MaNhanVienPhuTrach.Value != maNhanVien)
                return Json(new { success = false, message = "Cuộc trò chuyện đang do Nhân viên khác phụ trách." });

            if (!cuoc.MaNhanVienPhuTrach.HasValue)
                cuoc.MaNhanVienPhuTrach = maNhanVien;

            cuoc.TrangThai = "Nhân viên đang hỗ trợ";
            cuoc.NgayCapNhat = DateTime.Now;

            db.TinNhans.Add(new TinNhan
            {
                MaCuocTroChuyen = id,
                MaNguoiGui = maNhanVien,
                LoaiNguoiGui = "NhanVien",
                NoiDung = noiDung,
                DaDoc = false,
                ThoiGianGui = DateTime.Now
            });

            TaoThongBao(
                cuoc.MaKhachHang,
                "Nhân viên vừa phản hồi",
                "Nhân viên vừa phản hồi cuộc trò chuyện #" + cuoc.MaCuocTroChuyen + ".",
                Url.Action("Index", "DatSan")
            );

            db.SaveChanges();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult DongCuocTroChuyen(int id)
        {
            if (!DaDangNhap() || LayVaiTro() != "NhanVien")
                return Json(new { success = false });

            int maNhanVien = LayMaNguoiDung().Value;
            var cuoc = db.CuocTroChuyens.FirstOrDefault(x => x.MaCuocTroChuyen == id);

            if (cuoc == null)
                return Json(new { success = false });

            if (cuoc.MaNhanVienPhuTrach.HasValue && cuoc.MaNhanVienPhuTrach.Value != maNhanVien)
                return Json(new { success = false, message = "Bạn không phụ trách cuộc trò chuyện này." });

            cuoc.TrangThai = "Đã đóng";
            cuoc.NgayCapNhat = DateTime.Now;

            db.TinNhans.Add(new TinNhan
            {
                MaCuocTroChuyen = id,
                MaNguoiGui = maNhanVien,
                LoaiNguoiGui = "NhanVien",
                NoiDung = "Cuộc trò chuyện đã được kết thúc. Bạn có thể mở cuộc trò chuyện mới khi cần hỗ trợ thêm.",
                DaDoc = false,
                ThoiGianGui = DateTime.Now
            });

            TaoThongBao(
                cuoc.MaKhachHang,
                "Cuộc trò chuyện đã kết thúc",
                "Nhân viên đã kết thúc cuộc trò chuyện hỗ trợ của bạn.",
                Url.Action("Index", "DatSan")
            );

            db.SaveChanges();
            return Json(new { success = true });
        }

        [HttpGet]
        public JsonResult DemChatChoNhanVien()
        {
            if (!DaDangNhap() || LayVaiTro() != "NhanVien")
                return Json(new { success = false, count = 0 }, JsonRequestBehavior.AllowGet);

            int count = db.CuocTroChuyens.Count(x => x.TrangThai == "Chờ nhân viên");
            return Json(new { success = true, count = count }, JsonRequestBehavior.AllowGet);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}