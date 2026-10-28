using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QLKTX.Models;
using Microsoft.AspNetCore.Http;
using ClosedXML.Excel;
using System.IO;

namespace QLKTX.Controllers
{
    [AdminAuthorize]
    public class SinhVienController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SinhVienController(ApplicationDbContext context)
        {
            _context = context;
        }

        private SelectList BuildPhongSelectList(string? selectedMaPhong = null)
        {
            var phongs = _context.Phongs
                .Include(p => p.SinhViens)
                .AsNoTracking()
                .ToList()
                .Select(p =>
                {
                    int soDangO = p.SinhViens?.Count(s => s.TinhTrangLuuTru != "Đã rời đi") ?? 0;
                    int conTrong = Math.Max(0, p.SoLuongGiuong - soDangO);
                    return new
                    {
                        p.MaPhong,
                        ConTrong = conTrong,
                        IsSelected = p.MaPhong == selectedMaPhong,
                        DisplayText = conTrong > 0
                            ? $"P.{p.MaPhong} - {p.LoaiPhong} (Trống {conTrong}/{p.SoLuongGiuong} giường)"
                            : $"P.{p.MaPhong} - {p.LoaiPhong} (ĐÃ ĐẦY {soDangO}/{p.SoLuongGiuong})"
                    };
                })
                .Where(p => p.ConTrong > 0 || p.IsSelected)
                .ToList();

            return new SelectList(phongs, "MaPhong", "DisplayText", selectedMaPhong);
        }

        private async Task DongBoTrangThaiPhongAsync(string? maPhong)
        {
            if (string.IsNullOrEmpty(maPhong)) return;
            var phong = await _context.Phongs.FirstOrDefaultAsync(p => p.MaPhong == maPhong);
            if (phong == null) return;

            int soDangO = await _context.SinhViens.CountAsync(s => s.MaPhong == maPhong && s.TinhTrangLuuTru != "Đã rời đi");
            if (soDangO >= phong.SoLuongGiuong && phong.TinhTrang == "Còn chỗ")
            {
                phong.TinhTrang = "Đã đầy";
                _context.Phongs.Update(phong);
                await _context.SaveChangesAsync();
            }
            else if (soDangO < phong.SoLuongGiuong && phong.TinhTrang == "Đã đầy")
            {
                phong.TinhTrang = "Còn chỗ";
                _context.Phongs.Update(phong);
                await _context.SaveChangesAsync();
            }
        }

        // GET: SinhVien/Index
        public async Task<IActionResult> Index(string searchString)
        {
            DbInitializer.DongBoYeuCauPhongDaDuyet(_context);

            // Truyền lại từ khóa ra View để giữ lại chữ người dùng vừa gõ trong ô tìm kiếm
            ViewData["CurrentFilter"] = searchString;

            // Khởi tạo câu truy vấn (chỉ lấy sinh viên đang ở)
            var query = _context.SinhViens.Include(s => s.Phong).Where(s => s.TinhTrangLuuTru == "Đang ở").AsQueryable();

            // Nếu người dùng có gõ từ khóa tìm kiếm
            if (!string.IsNullOrEmpty(searchString))
            {
                // Chuẩn hóa từ khóa cho riêng tìm kiếm phòng: bỏ chữ 'P.' ở đầu do UI thêm vào tĩnh
                string phongSearch = searchString.Trim().ToUpper();
                if (phongSearch.StartsWith("P.")) phongSearch = phongSearch.Substring(2);
                else if (phongSearch.StartsWith("P ") || phongSearch.StartsWith("P-")) phongSearch = phongSearch.Substring(2);
                
                // Bỏ tiếp các dấu gạch, chấm, khoảng trắng để so sánh linh hoạt nhất
                phongSearch = phongSearch.Replace(".", "").Replace(" ", "").Replace("-", "");

                query = query.Where(s => 
                    s.MaSV.Contains(searchString)
                    || s.HoTen.Contains(searchString)
                    || s.Lop.Contains(searchString)
                    || s.SoDienThoai.Contains(searchString)
                    || (s.MaPhong != null && s.MaPhong.Replace(".", "").Replace(" ", "").Replace("-", "").ToUpper().Contains(phongSearch))
                );
            }

            // Thực thi truy vấn và lấy kết quả
            var danhSach = await query.ToListAsync();
            return View(danhSach);
        }

        // GET: SinhVien/LichSu
        public async Task<IActionResult> LichSu(string searchString)
        {
            DbInitializer.DongBoYeuCauPhongDaDuyet(_context);

            ViewData["CurrentFilter"] = searchString;

            // Lọc sinh viên "Đã rời đi"
            var query = _context.SinhViens.Include(s => s.Phong).Where(s => s.TinhTrangLuuTru == "Đã rời đi").AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                string phongSearch = searchString.Trim().ToUpper();
                if (phongSearch.StartsWith("P.")) phongSearch = phongSearch.Substring(2);
                else if (phongSearch.StartsWith("P ") || phongSearch.StartsWith("P-")) phongSearch = phongSearch.Substring(2);
                
                phongSearch = phongSearch.Replace(".", "").Replace(" ", "").Replace("-", "");

                query = query.Where(s => 
                    s.MaSV.Contains(searchString)
                    || s.HoTen.Contains(searchString)
                    || s.Lop.Contains(searchString)
                    || s.SoDienThoai.Contains(searchString)
                    || (s.MaPhong != null && s.MaPhong.Replace(".", "").Replace(" ", "").Replace("-", "").ToUpper().Contains(phongSearch))
                );
            }

            var danhSach = await query.ToListAsync();
            return View(danhSach);
        }

        // GET: SinhVien/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null) return NotFound();

            var sinhVien = await _context.SinhViens
                .Include(s => s.Phong)
                .FirstOrDefaultAsync(m => m.MaSV == id);

            if (sinhVien == null) return NotFound();

            return View(sinhVien);
        }

        // GET: SinhVien/Create
        [AdminAuthorize(OnlyAdmin = true)]
        public IActionResult Create()
        {
            ViewBag.Phongs = BuildPhongSelectList();
            return View();
        }

        // POST: SinhVien/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize(OnlyAdmin = true)]
        public async Task<IActionResult> Create(SinhVien sv)
        {
            ModelState.Remove("Phong");
            if (ModelState.IsValid)
            {
                if (_context.SinhViens.Any(e => e.MaSV == sv.MaSV))
                {
                    ModelState.AddModelError("MaSV", "Mã sinh viên đã tồn tại!");
                }
                else
                {
                    using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                    try
                    {
                        if (!string.IsNullOrEmpty(sv.MaPhong) && sv.TinhTrangLuuTru != "Đã rời đi")
                        {
                            var phong = await _context.Phongs.FirstOrDefaultAsync(p => p.MaPhong == sv.MaPhong);
                            if (phong != null)
                            {
                                int soDangO = await _context.SinhViens.CountAsync(s => s.MaPhong == sv.MaPhong && s.TinhTrangLuuTru == "Đang ở");
                                if (soDangO >= phong.SoLuongGiuong)
                                {
                                    await transaction.RollbackAsync();
                                    ModelState.AddModelError("MaPhong", $"Phòng P.{phong.MaPhong} đã đầy ({soDangO}/{phong.SoLuongGiuong} giường), vui lòng chọn phòng khác!");
                                    ViewBag.Phongs = BuildPhongSelectList(sv.MaPhong);
                                    return View(sv);
                                }
                            }
                        }

                        _context.Add(sv);
                        if (!_context.TaiKhoans.Any(t => t.TenDangNhap == sv.MaSV))
                        {
                            _context.TaiKhoans.Add(new TaiKhoan
                            {
                                TenDangNhap = sv.MaSV,
                                MatKhau = PasswordHelper.HashPassword("123"),
                                Quyen = "SinhVien",
                                MaSV = sv.MaSV
                            });
                        }
                        await _context.SaveChangesAsync();
                        await DongBoTrangThaiPhongAsync(sv.MaPhong);
                        await transaction.CommitAsync();

                        TempData["SuccessMessage"] = $"Đã tiếp nhận sinh viên {sv.HoTen} ({sv.MaSV}) thành công!";
                        return RedirectToAction(nameof(Index));
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
            ViewBag.Phongs = BuildPhongSelectList(sv.MaPhong);
            return View(sv);
        }

        // GET: SinhVien/Edit/5
        [AdminAuthorize(OnlyAdmin = true)]
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return NotFound();

            var sinhVien = await _context.SinhViens.FindAsync(id);
            if (sinhVien == null) return NotFound();

            ViewBag.MaPhong = BuildPhongSelectList(sinhVien.MaPhong);
            return View(sinhVien);
        }

        // POST: SinhVien/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize(OnlyAdmin = true)]
        public async Task<IActionResult> Edit(string id, SinhVien sv)
        {
            if (id != sv.MaSV) return NotFound();

            ModelState.Remove("Phong");
            if (ModelState.IsValid)
            {
                try
                {
                    var oldSv = await _context.SinhViens
                        .AsNoTracking()
                        .Where(s => s.MaSV == sv.MaSV)
                        .Select(s => new { s.MaPhong, s.TinhTrangLuuTru })
                        .FirstOrDefaultAsync();
                    string? maPhongCu = oldSv?.MaPhong;
                    string oldStatus = oldSv?.TinhTrangLuuTru ?? "";

                    if (!string.IsNullOrEmpty(sv.MaPhong) && sv.TinhTrangLuuTru != "Đã rời đi")
                    {
                        var phongMoi = await _context.Phongs.FirstOrDefaultAsync(p => p.MaPhong == sv.MaPhong);
                        if (phongMoi != null)
                        {
                            int soDangO = await _context.SinhViens.CountAsync(s => s.MaPhong == sv.MaPhong && s.TinhTrangLuuTru != "Đã rời đi" && s.MaSV != sv.MaSV);
                            if (soDangO >= phongMoi.SoLuongGiuong)
                            {
                                ModelState.AddModelError("MaPhong", $"Phòng P.{phongMoi.MaPhong} đã đạt tối đa {phongMoi.SoLuongGiuong}/{phongMoi.SoLuongGiuong} giường, không thể chuyển vào!");
                                ViewBag.MaPhong = BuildPhongSelectList(sv.MaPhong);
                                return View(sv);
                            }
                        }
                    }

                    // Đánh dấu mọi yêu cầu trả/chuyển phòng cũ của SV này đã đồng bộ để không ghi đè thao tác sửa tay của Admin
                    var oldYeuCaus = await _context.BaoCaoSuCos
                        .Where(b => b.MaSV == sv.MaSV && (b.MaSuCo.StartsWith("YC_") || b.TenSuCo.StartsWith("[")) && !b.MoTa.Contains("[Đã đồng bộ]"))
                        .ToListAsync();
                    foreach (var yc in oldYeuCaus)
                    {
                        yc.MoTa = (yc.MoTa ?? "") + " [Đã đồng bộ]";
                    }

                    _context.Update(sv);
                    await _context.SaveChangesAsync();

                    // Tự động đồng bộ trạng thái phòng cũ và phòng mới ("Còn chỗ" / "Đã đầy")
                    await DongBoTrangThaiPhongAsync(maPhongCu);
                    if (maPhongCu != sv.MaPhong)
                    {
                        await DongBoTrangThaiPhongAsync(sv.MaPhong);
                    }
                    // Khi đổi trạng thái sang "Đình chỉ" hoặc "Kỷ luật": Tự động chuyển/ghi nhận sang danh sách Vi phạm & Xử lý
                    // Lưu ý: KHÔNG tự động giải phóng chỗ ở hay giảm sĩ số phòng, sinh viên vẫn giữ chỗ ở phòng KTX
                    if (sv.TinhTrangLuuTru == "Đình chỉ" || sv.TinhTrangLuuTru == "Kỷ luật")
                    {
                        bool isStatusChanged = oldStatus != sv.TinhTrangLuuTru;
                        bool hasViPham = await _context.ViPhams.AnyAsync(v => v.MaSV == sv.MaSV);

                        if (isStatusChanged || !hasViPham)
                        {
                            var viPhamMoi = new ViPham
                            {
                                MaSV = sv.MaSV,
                                NgayViPham = DateTime.Now,
                                HinhThucXuLy = sv.TinhTrangLuuTru,
                                NoiDungViPham = sv.TinhTrangLuuTru == "Đình chỉ"
                                    ? "Đình chỉ lưu trú do vi phạm quy chế KTX"
                                    : "Kỷ luật cảnh cáo do vi phạm nội quy KTX",
                                GhiChu = $"Chuyển từ cập nhật hồ sơ sinh viên (Phòng P.{sv.MaPhong ?? "Chưa xếp"})"
                            };
                            _context.ViPhams.Add(viPhamMoi);
                            await _context.SaveChangesAsync();
                        }

                        TempData["SuccessMessage"] = $"Đã cập nhật hồ sơ và chuyển sinh viên {sv.HoTen} ({sv.MaSV}) sang danh sách Vi phạm & Xử lý thành công!";
                        return RedirectToAction("Index", "ViPham");
                    }

                    TempData["SuccessMessage"] = $"Đã cập nhật hồ sơ sinh viên {sv.HoTen} ({sv.MaSV})!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.SinhViens.Any(e => e.MaSV == sv.MaSV)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.MaPhong = BuildPhongSelectList(sv.MaPhong);
            return View(sv);
        }

        // GET: SinhVien/Delete/5
        [AdminAuthorize(OnlyAdmin = true)]
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null) return NotFound();

            var sinhVien = await _context.SinhViens
                .Include(s => s.Phong)
                .FirstOrDefaultAsync(m => m.MaSV == id);

            if (sinhVien == null) return NotFound();

            return View(sinhVien);
        }

        // POST: SinhVien/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [AdminAuthorize(OnlyAdmin = true)]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var sinhVien = await _context.SinhViens.FindAsync(id);
            if (sinhVien != null)
            {
                sinhVien.TinhTrangLuuTru = "Đã rời đi";
                _context.SinhViens.Update(sinhVien);
                await _context.SaveChangesAsync();

                await DongBoTrangThaiPhongAsync(sinhVien.MaPhong);
                TempData["SuccessMessage"] = $"Đã chuyển sinh viên {sinhVien.HoTen} ({sinhVien.MaSV}) sang danh sách Lịch sử rời KTX!";
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: SinhVien/TaoDuLieuMau - Chặn xóa CSDL qua GET
        [HttpGet]
        [AdminAuthorize(OnlyAdmin = true)]
        public IActionResult TaoDuLieuMau()
        {
            TempData["ErrorMessage"] = "Cảnh báo bảo mật: Thao tác tạo dữ liệu mẫu bắt buộc phải thực thi qua phương thức POST để bảo vệ CSDL!";
            return RedirectToAction(nameof(Index));
        }

        // POST: SinhVien/TaoDuLieuMau
        [HttpPost, ActionName("TaoDuLieuMau")]
        [ValidateAntiForgeryToken]
        [AdminAuthorize(OnlyAdmin = true)]
        public IActionResult TaoDuLieuMauConfirmed()
        {
            try
            {
                _context.ViPhams.RemoveRange(_context.ViPhams);
                _context.HopDongs.RemoveRange(_context.HopDongs);
                _context.HoaDons.RemoveRange(_context.HoaDons);
                _context.BaoCaoSuCos.RemoveRange(_context.BaoCaoSuCos);
                _context.CoSoVatChats.RemoveRange(_context.CoSoVatChats);
                _context.SaveChanges();

                _context.SinhViens.RemoveRange(_context.SinhViens);
                _context.SaveChanges();

                _context.Phongs.RemoveRange(_context.Phongs);
                _context.SaveChanges();

                var random = new Random();

                var phongs = new List<Phong>();
                for (int tang = 1; tang <= 5; tang++)
                {
                    for (int i = 1; i <= 15; i++)
                    {
                        string maPhong = $"{tang}{i:D2}";
                        int loai = random.Next(1, 4);

                        phongs.Add(new Phong
                        {
                            MaPhong = maPhong,
                            LoaiPhong = loai == 1 ? "Phòng thường" : (loai == 2 ? "Phòng có điều hoà" : "Phòng điều hoà + nóng lạnh"),
                            SoLuongGiuong = loai == 3 ? 4 : 6,
                            GiaPhong = loai == 1 ? 350000m : (loai == 2 ? 650000m : 950000m),
                            TinhTrang = "Còn chỗ",
                            TrangThaiThietBi = "Bình thường"
                        });
                    }
                }
                _context.Phongs.AddRange(phongs);
                _context.SaveChanges();

                var coSoVatChats = new List<CoSoVatChat>();
                foreach (var p in phongs)
                {
                    coSoVatChats.Add(new CoSoVatChat { MaThietBi = $"Giuong{p.MaPhong}", TenThietBi = "Giường", LoaiThietBi = "Nội thất", SoLuong = 6, MaPhong = p.MaPhong, TinhTrang = "Đang sử dụng" });
                    coSoVatChats.Add(new CoSoVatChat { MaThietBi = $"BongDen{p.MaPhong}", TenThietBi = "Bóng đèn", LoaiThietBi = "Thiết bị điện", SoLuong = 4, MaPhong = p.MaPhong, TinhTrang = "Đang sử dụng" });
                    coSoVatChats.Add(new CoSoVatChat { MaThietBi = $"QuatTran{p.MaPhong}", TenThietBi = "Quạt trần", LoaiThietBi = "Thiết bị điện", SoLuong = 1, MaPhong = p.MaPhong, TinhTrang = "Đang sử dụng" });
                    coSoVatChats.Add(new CoSoVatChat { MaThietBi = $"BonRuaMat{p.MaPhong}", TenThietBi = "Bồn rửa mặt", LoaiThietBi = "Thiết bị vệ sinh", SoLuong = 2, MaPhong = p.MaPhong, TinhTrang = "Đang sử dụng" });
                    coSoVatChats.Add(new CoSoVatChat { MaThietBi = $"BonCau{p.MaPhong}", TenThietBi = "Bồn cầu", LoaiThietBi = "Thiết bị vệ sinh", SoLuong = 1, MaPhong = p.MaPhong, TinhTrang = "Đang sử dụng" });
                    coSoVatChats.Add(new CoSoVatChat { MaThietBi = $"VoiHoaSen{p.MaPhong}", TenThietBi = "Vòi hoa sen", LoaiThietBi = "Thiết bị vệ sinh", SoLuong = 1, MaPhong = p.MaPhong, TinhTrang = "Đang sử dụng" });
                    coSoVatChats.Add(new CoSoVatChat { MaThietBi = $"Guong{p.MaPhong}", TenThietBi = "Gương", LoaiThietBi = "Nội thất", SoLuong = 2, MaPhong = p.MaPhong, TinhTrang = "Đang sử dụng" });

                    if (p.LoaiPhong == "Phòng có điều hoà" || p.LoaiPhong == "Phòng điều hoà + nóng lạnh")
                    {
                        coSoVatChats.Add(new CoSoVatChat { MaThietBi = $"DieuHoa{p.MaPhong}", TenThietBi = "Điều hoà", LoaiThietBi = "Điện lạnh", SoLuong = 1, MaPhong = p.MaPhong, TinhTrang = "Đang sử dụng" });
                    }
                    if (p.LoaiPhong == "Phòng điều hoà + nóng lạnh")
                    {
                        coSoVatChats.Add(new CoSoVatChat { MaThietBi = $"NongLanh{p.MaPhong}", TenThietBi = "Bình nóng lạnh", LoaiThietBi = "Điện lạnh", SoLuong = 1, MaPhong = p.MaPhong, TinhTrang = "Đang sử dụng" });
                    }
                }
                _context.CoSoVatChats.AddRange(coSoVatChats);
                _context.SaveChanges();

                var sinhViens = new List<SinhVien>();
                string[] ho = { "Nguyễn", "Trần", "Lê", "Phạm", "Hoàng", "Huỳnh", "Vũ", "Đặng", "Bùi", "Đỗ" };
                string[] dem = { "Văn", "Thị", "Hữu", "Đức", "Ngọc", "Thanh", "Minh", "Thu", "Hải", "Xuân" };
                string[] ten = { "An", "Anh", "Bảo", "Chi", "Dũng", "Duy", "Đạt", "Giang", "Hà", "Hải", "Linh", "Long", "Đôn", "Kiên", "Chung", "Duy" };
                string[] nganh = { "KTMT", "CNTT", "KTPM", "HTTT", "ATTT" };
                string[] hauTo = { "A", "B", "C", "N", "M" };

                int svIdCounter = 1;
                foreach (var p in phongs)
                {
                    int soSinhVienHienTai = random.Next(p.SoLuongGiuong - 2, p.SoLuongGiuong + 1);
                    if (soSinhVienHienTai < 0) soSinhVienHienTai = 0;

                    for (int i = 0; i < soSinhVienHienTai; i++)
                    {
                        if (svIdCounter > 999) break;
                        string tenLop = $"{nganh[random.Next(nganh.Length)]} K{random.Next(20, 25)}{hauTo[random.Next(hauTo.Length)]}";

                        sinhViens.Add(new SinhVien
                        {
                            MaSV = $"DTC{svIdCounter:D3}",
                            HoTen = $"{ho[random.Next(ho.Length)]} {dem[random.Next(dem.Length)]} {ten[random.Next(ten.Length)]}",
                            NgaySinh = new DateTime(random.Next(2003, 2006), random.Next(1, 13), random.Next(1, 28)),
                            Lop = tenLop,
                            SoDienThoai = $"0{random.Next(3, 9)}{random.Next(10000000, 99999999)}",
                            CCCD = $"0272{random.Next(10000000, 99999999)}",
                            TinhTrangLuuTru = "Đang ở",
                            MaPhong = p.MaPhong
                        });
                        svIdCounter++;
                    }
                    if (soSinhVienHienTai >= p.SoLuongGiuong) p.TinhTrang = "Đã đầy";
                }
                _context.SinhViens.AddRange(sinhViens);
                _context.SaveChanges();

                var hopDongs = new List<HopDong>();
                int hdCounter = 1;
                foreach (var sv in sinhViens)
                {
                    hopDongs.Add(new HopDong
                    {
                        MaHopDong = $"HD{hdCounter:D3}",
                        MaSV = sv.MaSV,
                        MaPhong = sv.MaPhong,
                        NgayBatDau = new DateTime(2025, 8, random.Next(1, 30)),
                        NgayKetThuc = new DateTime(2026, 6, 30),
                        TienDatCoc = 1000000m,
                        TrangThai = "Còn hiệu lực",
                        DieuKhoan = "Sinh viên đã nộp đủ tiền cọc và ký cam kết nội quy KTX."
                    });
                    hdCounter++;
                }
                _context.HopDongs.AddRange(hopDongs);
                _context.SaveChanges();

                var viPhams = new List<ViPham>();
                string[] loiViPham = {
                    "Về muộn quá giờ quy định",
                    "Dẫn bạn bên ngoài vào phòng ngủ qua đêm",
                    "Nấu ăn trong phòng",
                    "Chửi tục, gây rối trật tự",
                    "Làm ồn sau 22:00",
                    "Không vệ sinh hành lang và phòng ở"
                };
                string[] hinhThuc = { "Nhắc nhở", "Cảnh cáo", "Phạt tiền", "Đình chỉ" };

                DateTime startDate = new DateTime(2025, 1, 1);
                DateTime endDate = new DateTime(2026, 3, 31);
                int totalDays = (endDate - startDate).Days;

                int soLuongViPham = random.Next(50, 81);
                for (int i = 0; i < soLuongViPham; i++)
                {
                    var svViPham = sinhViens[random.Next(sinhViens.Count)];
                    DateTime ngayBiPhat = startDate.AddDays(random.Next(totalDays));
                    string loi = loiViPham[random.Next(loiViPham.Length)];
                    string phat = hinhThuc[random.Next(hinhThuc.Length)];

                    viPhams.Add(new ViPham
                    {
                        MaSV = svViPham.MaSV,
                        NoiDungViPham = loi,
                        HinhThucXuLy = phat,
                        NgayViPham = ngayBiPhat,
                        GhiChu = "Ban quản lý tuần tra phát hiện."
                    });

                    if (phat == "Đình chỉ")
                    {
                        svViPham.TinhTrangLuuTru = "Đình chỉ";
                    }
                }
                _context.ViPhams.AddRange(viPhams);
                _context.SaveChanges();

                return Content("BỘ DỮ LIỆU KHỔNG LỒ ĐÃ ĐƯỢC TẠO THÀNH CÔNG! Bao gồm Sinh viên, Phòng, CSVC, Hợp đồng và Kỷ luật. Hãy F5 để tận hưởng!");
            }
            catch (Exception ex)
            {
                return Content($"CÓ LỖI XẢY RA KHI LƯU DỮ LIỆU:\n{ex.Message}\nCHI TIẾT: {ex.InnerException?.Message}");
            }
        }

        [HttpPost]
        [AdminAuthorize(OnlyAdmin = true)]
        public async Task<IActionResult> ImportExcel(IFormFile fileExcel)
        {
            if (fileExcel == null || fileExcel.Length == 0)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn file Excel để tải lên!";
                return RedirectToAction("Index");
            }

            try
            {
                var sinhVienList = new List<SinhVien>();
                var taiKhoanList = new List<TaiKhoan>();
                int soDongThanhCong = 0;

                using (var stream = new MemoryStream())
                {
                    await fileExcel.CopyToAsync(stream);
                    using (var workBook = new XLWorkbook(stream))
                    {
                        var workSheet = workBook.Worksheet(1);
                        var firstRowUsed = workSheet.FirstRowUsed();
                        var lastRowUsed = workSheet.LastRowUsed();

                        for (int row = 2; row <= lastRowUsed.RowNumber(); row++)
                        {
                            string maSV = workSheet.Cell(row, 1).Value.ToString().Trim();
                            string hoTen = workSheet.Cell(row, 2).Value.ToString().Trim();
                            string ngaySinhStr = workSheet.Cell(row, 3).Value.ToString().Trim();
                            string lop = workSheet.Cell(row, 4).Value.ToString().Trim();

                            string sdt = workSheet.Cell(row, 5).Value.ToString().Trim();
                            if (!string.IsNullOrEmpty(sdt) && !sdt.StartsWith("0")) { sdt = "0" + sdt; }

                            string cccd = workSheet.Cell(row, 6).Value.ToString().Trim();
                            if (!string.IsNullOrEmpty(cccd) && !cccd.StartsWith("0")) { cccd = "0" + cccd; }

                            if (string.IsNullOrEmpty(maSV)) continue;

                            bool isExist = _context.SinhViens.Any(s => s.MaSV == maSV);
                            if (!isExist)
                            {
                                var sv = new SinhVien
                                {
                                    MaSV = maSV,
                                    HoTen = hoTen,
                                    Lop = lop,
                                    SoDienThoai = sdt,
                                    CCCD = cccd,
                                    TinhTrangLuuTru = "Đang ở",
                                    NgaySinh = DateTime.TryParse(ngaySinhStr, out DateTime ns) ? ns : new DateTime(2000, 1, 1)
                                };
                                sinhVienList.Add(sv);

                                var tk = new TaiKhoan
                                {
                                    TenDangNhap = maSV,
                                    MatKhau = "123",
                                    Quyen = "SinhVien",
                                    MaSV = maSV
                                };
                                taiKhoanList.Add(tk);

                                soDongThanhCong++;
                            }
                        }
                    }
                }

                if (sinhVienList.Any())
                {
                    _context.SinhViens.AddRange(sinhVienList);
                    _context.TaiKhoans.AddRange(taiKhoanList);
                    await _context.SaveChangesAsync();
                }

                TempData["SuccessMessage"] = $"Import thành công {soDongThanhCong} sinh viên và tạo tài khoản mặc định (Mật khẩu: 123)!";
            }
            catch (Exception ex)
            {
                string loiChiTiet = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                TempData["ErrorMessage"] = "Lỗi từ Database: " + loiChiTiet;
            }

            return RedirectToAction("Index");
        }

        // GET: SinhVien/ExportToExcel
        [HttpGet]
        public async Task<IActionResult> ExportToExcel()
        {
            var danhSach = await _context.SinhViens
                .Include(s => s.Phong)
                .Where(s => s.TinhTrangLuuTru == "Đang ở")
                .OrderBy(s => s.MaPhong)
                .ThenBy(s => s.MaSV)
                .AsNoTracking()
                .ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("DanhSachSinhVienKTX");

                worksheet.Cell(1, 1).Value = "STT";
                worksheet.Cell(1, 2).Value = "Mã SV";
                worksheet.Cell(1, 3).Value = "Họ và Tên";
                worksheet.Cell(1, 4).Value = "Ngày Sinh";
                worksheet.Cell(1, 5).Value = "Lớp";
                worksheet.Cell(1, 6).Value = "Mã Phòng";
                worksheet.Cell(1, 7).Value = "Loại Phòng";
                worksheet.Cell(1, 8).Value = "Số Điện Thoại";
                worksheet.Cell(1, 9).Value = "CCCD";
                worksheet.Cell(1, 10).Value = "Tình Trạng Lưu Trú";

                var headerRow = worksheet.Row(1);
                headerRow.Style.Font.Bold = true;
                headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#4F46E5");
                headerRow.Style.Font.FontColor = XLColor.White;
                headerRow.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                worksheet.Column(8).Style.NumberFormat.Format = "@";
                worksheet.Column(9).Style.NumberFormat.Format = "@";

                int rowIdx = 2;
                int stt = 1;
                foreach (var sv in danhSach)
                {
                    worksheet.Cell(rowIdx, 1).Value = stt++;
                    worksheet.Cell(rowIdx, 2).Value = sv.MaSV;
                    worksheet.Cell(rowIdx, 3).Value = sv.HoTen;
                    worksheet.Cell(rowIdx, 4).Value = sv.NgaySinh.ToString("yyyy-MM-dd");
                    worksheet.Cell(rowIdx, 5).Value = sv.Lop;
                    worksheet.Cell(rowIdx, 6).Value = sv.MaPhong ?? "Chưa xếp";
                    worksheet.Cell(rowIdx, 7).Value = sv.Phong?.LoaiPhong ?? "-";
                    worksheet.Cell(rowIdx, 8).Value = sv.SoDienThoai;
                    worksheet.Cell(rowIdx, 9).Value = sv.CCCD;
                    worksheet.Cell(rowIdx, 10).Value = sv.TinhTrangLuuTru;
                    rowIdx++;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    string fileName = $"DanhSach_SinhVien_KTX_{DateTime.Now:yyyyMMdd}.xlsx";
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
        }

        // GET: SinhVien/DownloadTemplate
        public IActionResult DownloadTemplate()
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("SinhVien");

                worksheet.Cell(1, 1).Value = "Mã SV";
                worksheet.Cell(1, 2).Value = "Họ Tên";
                worksheet.Cell(1, 3).Value = "Ngày Sinh (yyyy-MM-dd)";
                worksheet.Cell(1, 4).Value = "Lớp";
                worksheet.Cell(1, 5).Value = "Số điện thoại";
                worksheet.Cell(1, 6).Value = "CCCD";

                var headerRow = worksheet.Row(1);
                headerRow.Style.Font.Bold = true;
                headerRow.Style.Fill.BackgroundColor = XLColor.LightGreen;
                headerRow.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                worksheet.Column(5).Style.NumberFormat.Format = "@";
                worksheet.Column(6).Style.NumberFormat.Format = "@";

                worksheet.Cell(2, 1).Value = "DTC999";
                worksheet.Cell(2, 2).Value = "Nguyễn Văn Mẫu";
                worksheet.Cell(2, 3).Value = "2004-01-01";
                worksheet.Cell(2, 4).Value = "CNTT K22A";
                worksheet.Cell(2, 5).Value = "0123456789";
                worksheet.Cell(2, 6).Value = "027204000123";

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Mau_Nhap_SinhVien.xlsx");
                }
            }
        }
    }
}
