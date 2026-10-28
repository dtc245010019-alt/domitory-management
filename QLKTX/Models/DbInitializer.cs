using QLKTX.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace QLKTX
{
    public static class DbInitializer
    {
        public static void Initialize(ApplicationDbContext context)
        {
            var random = new Random(2026);

            // =========================================================================
            // 1. BỔ SUNG ĐẦY ĐỦ DANH SÁCH PHÒNG (TẦNG 1..5 & KHU TÒA A, B, C)
            // =========================================================================
            var existingPhongIds = context.Phongs.Select(p => p.MaPhong).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var newPhongs = new List<Phong>();

            // Tạo phòng theo Tầng 1 -> 5 (101..512)
            for (int tang = 1; tang <= 5; tang++)
            {
                for (int i = 1; i <= 12; i++)
                {
                    string maPhong = $"{tang}{i:D2}";
                    if (existingPhongIds.Contains(maPhong)) continue;

                    int loai = (tang + i) % 3;
                    string loaiPhong = loai == 0 ? "Phòng thường" : (loai == 1 ? "Phòng có điều hoà" : "Phòng điều hoà + nóng lạnh");
                    // Chuẩn hóa biểu phí theo thị trường KTX thực tế (tính theo đầu sinh viên / tháng):
                    // - Phòng thường: 350.000 VNĐ / SV / tháng (1.750.000 VNĐ / kỳ 5 tháng)
                    // - Phòng có điều hoà: 650.000 VNĐ / SV / tháng (3.250.000 VNĐ / kỳ 5 tháng)
                    // - Phòng điều hoà + nóng lạnh: 950.000 VNĐ / SV / tháng (4.750.000 VNĐ / kỳ 5 tháng)
                    decimal giaPhong = loai == 0 ? 350000m : (loai == 1 ? 650000m : 950000m);
                    int soGiuong = (loai == 2) ? 4 : ((loai == 1) ? ((i % 2 == 0) ? 4 : 6) : ((i % 4 == 0) ? 8 : 6));

                    newPhongs.Add(new Phong
                    {
                        MaPhong = maPhong,
                        LoaiPhong = loaiPhong,
                        SoLuongGiuong = soGiuong,
                        GiaPhong = giaPhong,
                        TinhTrang = (i == 12 && tang >= 4) ? "Bảo trì" : "Còn chỗ",
                        TrangThaiThietBi = (i == 12 && tang >= 4) ? "Đang bảo trì định kỳ" : "Bình thường"
                    });
                    existingPhongIds.Add(maPhong);
                }
            }

            // Tạo thêm phòng theo Khu Tòa A1, B1, C1 (A1-01..C1-08)
            string[] khuToa = { "A1", "B1", "C1" };
            foreach (var toa in khuToa)
            {
                for (int i = 1; i <= 8; i++)
                {
                    string maPhong = $"{toa}-{i:D2}";
                    if (existingPhongIds.Contains(maPhong)) continue;

                    int loai = i % 3;
                    string loaiPhong = loai == 0 ? "Phòng thường" : (loai == 1 ? "Phòng có điều hoà" : "Phòng điều hoà + nóng lạnh");
                    decimal giaPhong = loai == 0 ? 350000m : (loai == 1 ? 650000m : 950000m);
                    int soGiuong = (loai == 2) ? 4 : 6;

                    newPhongs.Add(new Phong
                    {
                        MaPhong = maPhong,
                        LoaiPhong = loaiPhong,
                        SoLuongGiuong = soGiuong,
                        GiaPhong = giaPhong,
                        TinhTrang = "Còn chỗ",
                        TrangThaiThietBi = "Bình thường"
                    });
                    existingPhongIds.Add(maPhong);
                }
            }

            if (newPhongs.Any())
            {
                context.Phongs.AddRange(newPhongs);
                context.SaveChanges();
            }

            var allPhongs = context.Phongs.ToList();

            // =========================================================================
            // CHUẨN HÓA DỮ LIỆU CŨ NẾU CSDL ĐÃ TỒN TẠI BIỂU PHÍ CŨ
            // =========================================================================
            bool hasPhongPriceFix = false;
            foreach (var p in allPhongs)
            {
                decimal standardPrice = 350000m;
                int standardGiuong = p.SoLuongGiuong;
                if (p.LoaiPhong.Contains("nóng lạnh", StringComparison.OrdinalIgnoreCase))
                {
                    standardPrice = 950000m;
                    standardGiuong = 4;
                }
                else if (p.LoaiPhong.Contains("điều hoà", StringComparison.OrdinalIgnoreCase))
                {
                    standardPrice = 650000m;
                    if (standardGiuong != 4 && standardGiuong != 6) standardGiuong = 6;
                }
                else
                {
                    standardPrice = 350000m;
                    if (standardGiuong != 6 && standardGiuong != 8) standardGiuong = 6;
                }

                if (p.GiaPhong != standardPrice || p.SoLuongGiuong != standardGiuong)
                {
                    p.GiaPhong = standardPrice;
                    p.SoLuongGiuong = standardGiuong;
                    hasPhongPriceFix = true;
                }
            }
            if (hasPhongPriceFix)
            {
                context.SaveChanges();
            }

            // Chuẩn hóa tiền cọc hợp đồng cũ về mức chuẩn 1.000.000 VNĐ
            var oldHopDongs = context.HopDongs.Where(h => h.TienDatCoc != 1000000m).ToList();
            if (oldHopDongs.Any())
            {
                foreach (var hd in oldHopDongs)
                {
                    hd.TienDatCoc = 1000000m;
                }
                context.SaveChanges();
            }

            // =========================================================================
            // 2. BỔ SUNG HỒ SƠ SINH VIÊN (ĐANG Ở + LỊCH SỬ RỜI ĐI + ĐÌNH CHỈ)
            // =========================================================================
            var existingSvIds = context.SinhViens.Select(s => s.MaSV).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var newSinhViens = new List<SinhVien>();

            string[] ho = { "Nguyễn", "Trần", "Lê", "Phạm", "Hoàng", "Huỳnh", "Vũ", "Võ", "Đặng", "Bùi", "Đỗ", "Hồ", "Ngô", "Dương", "Lý" };
            string[] dem = { "Văn", "Thị", "Hữu", "Đức", "Ngọc", "Thanh", "Minh", "Thu", "Hải", "Xuân", "Gia", "Bảo", "Quốc", "Tuấn" };
            string[] ten = { "An", "Anh", "Bảo", "Chi", "Dũng", "Duy", "Đạt", "Giang", "Hà", "Hải", "Hiếu", "Hùng", "Hương", "Khang", "Khánh", "Khoa", "Kiên", "Lâm", "Lan", "Linh", "Long", "Mai", "Minh", "Nam", "Nga", "Ngọc", "Nhi", "Phúc", "Phương", "Quân", "Quang", "Sơn", "Tâm", "Thảo", "Thắng", "Thành", "Trang", "Trung", "Tuấn", "Uyên", "Vân", "Việt", "Vinh", "Yến" };
            string[] nganh = { "CNTT", "KTPM", "HTTT", "ATTT", "KTMT", "TMDT", "KHMT" };
            string[] hauTo = { "A", "B", "C" };

            int svCounter = 1;
            var activeRooms = allPhongs.Where(p => p.TinhTrang != "Bảo trì").ToList();

            if (existingSvIds.Count < 180)
            {
                foreach (var p in activeRooms)
                {
                    int currentInRoom = context.SinhViens.Count(s => s.MaPhong == p.MaPhong && s.TinhTrangLuuTru == "Đang ở");
                    int targetInRoom = random.Next(Math.Max(2, p.SoLuongGiuong - 2), p.SoLuongGiuong + 1);

                    while (currentInRoom < targetInRoom && svCounter <= 260)
                    {
                        string maSV = $"DTC{svCounter:D3}";
                        svCounter++;
                        if (existingSvIds.Contains(maSV)) continue;

                        newSinhViens.Add(new SinhVien
                        {
                            MaSV = maSV,
                            HoTen = $"{ho[random.Next(ho.Length)]} {dem[random.Next(dem.Length)]} {ten[random.Next(ten.Length)]}",
                            NgaySinh = new DateTime(random.Next(2003, 2008), random.Next(1, 13), random.Next(1, 28)),
                            Lop = $"{nganh[random.Next(nganh.Length)]} K{random.Next(20, 25)}{hauTo[random.Next(hauTo.Length)]}",
                            SoDienThoai = $"0{random.Next(3, 9)}{random.Next(10000000, 99999999)}",
                            CCCD = $"0272{random.Next(10000000, 99999999)}",
                            TinhTrangLuuTru = "Đang ở",
                            MaPhong = p.MaPhong
                        });
                        existingSvIds.Add(maSV);
                        currentInRoom++;
                    }
                }

                // Tạo thêm 25 sinh viên cho danh sách "Lịch sử rời KTX" (Đã rời đi)
                for (int k = 1; k <= 25; k++)
                {
                    string maSV = $"DTC{800 + k:D3}";
                    if (existingSvIds.Contains(maSV)) continue;

                    var randomPhong = activeRooms[random.Next(activeRooms.Count)];
                    newSinhViens.Add(new SinhVien
                    {
                        MaSV = maSV,
                        HoTen = $"{ho[random.Next(ho.Length)]} {dem[random.Next(dem.Length)]} {ten[random.Next(ten.Length)]}",
                        NgaySinh = new DateTime(random.Next(2002, 2006), random.Next(1, 13), random.Next(1, 28)),
                        Lop = $"{nganh[random.Next(nganh.Length)]} K{random.Next(19, 23)}{hauTo[random.Next(hauTo.Length)]}",
                        SoDienThoai = $"0{random.Next(3, 9)}{random.Next(10000000, 99999999)}",
                        CCCD = $"0272{random.Next(10000000, 99999999)}",
                        TinhTrangLuuTru = "Đã rời đi",
                        MaPhong = randomPhong.MaPhong
                    });
                    existingSvIds.Add(maSV);
                }

                if (newSinhViens.Any())
                {
                    context.SinhViens.AddRange(newSinhViens);
                    context.SaveChanges();
                }
            }

            // Đồng bộ chuẩn xác trạng thái "Đã đầy" / "Còn chỗ" cho toàn bộ phòng
            var svDangOByPhong = context.SinhViens
                .Where(s => s.TinhTrangLuuTru == "Đang ở" && s.MaPhong != null)
                .GroupBy(s => s.MaPhong!)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var p in allPhongs)
            {
                if (p.TinhTrang == "Bảo trì") continue;
                int count = svDangOByPhong.TryGetValue(p.MaPhong, out int c) ? c : 0;
                p.TinhTrang = count >= p.SoLuongGiuong ? "Đã đầy" : "Còn chỗ";
            }
            context.SaveChanges();

            var allSinhViens = context.SinhViens.ToList();

            // =========================================================================
            // 3. TÀI KHOẢN ĐĂNG NHẬP (ADMIN, KẾ TOÁN & TOÀN BỘ SINH VIÊN)
            // =========================================================================
            var existingUsernames = context.TaiKhoans.Select(t => t.TenDangNhap).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var newTaiKhoans = new List<TaiKhoan>();

            if (!existingUsernames.Contains("admin"))
            {
                newTaiKhoans.Add(new TaiKhoan { TenDangNhap = "admin", MatKhau = "123", Quyen = "Admin", MaSV = null });
                existingUsernames.Add("admin");
            }
            if (!existingUsernames.Contains("ketoan"))
            {
                newTaiKhoans.Add(new TaiKhoan { TenDangNhap = "ketoan", MatKhau = "123", Quyen = "KeToan", MaSV = null });
                existingUsernames.Add("ketoan");
            }
            foreach (var sv in allSinhViens)
            {
                if (!existingUsernames.Contains(sv.MaSV))
                {
                    newTaiKhoans.Add(new TaiKhoan { TenDangNhap = sv.MaSV, MatKhau = "123", Quyen = "SinhVien", MaSV = sv.MaSV });
                    existingUsernames.Add(sv.MaSV);
                }
            }
            if (newTaiKhoans.Any())
            {
                context.TaiKhoans.AddRange(newTaiKhoans);
                context.SaveChanges();
            }

            // =========================================================================
            // 4. HỢP ĐỒNG LƯU TRÚ (HopDongs - Đảm bảo >= 120 bản ghi)
            // =========================================================================
            var existingHdIds = context.HopDongs.Select(h => h.MaHopDong).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (existingHdIds.Count < 100)
            {
                var newHopDongs = new List<HopDong>();
                int hdIndex = 1;
                foreach (var sv in allSinhViens.Take(160))
                {
                    string maHd = $"HD{hdIndex:D3}";
                    hdIndex++;
                    if (existingHdIds.Contains(maHd) || string.IsNullOrEmpty(sv.MaPhong)) continue;

                    bool isConHieuLuc = sv.TinhTrangLuuTru == "Đang ở";
                    newHopDongs.Add(new HopDong
                    {
                        MaHopDong = maHd,
                        MaSV = sv.MaSV,
                        MaPhong = sv.MaPhong,
                        NgayBatDau = isConHieuLuc ? new DateTime(2025, 8, random.Next(15, 30)) : new DateTime(2024, 8, 15),
                        NgayKetThuc = isConHieuLuc ? new DateTime(2026, 6, 30) : new DateTime(2025, 6, 30),
                        TienDatCoc = 1000000m,
                        TrangThai = isConHieuLuc ? "Còn hiệu lực" : "Hết hiệu lực",
                        DieuKhoan = "Sinh viên đã nộp đủ tiền đặt cọc và cam kết thực hiện đúng nội quy KTX."
                    });
                    existingHdIds.Add(maHd);
                }
                if (newHopDongs.Any())
                {
                    context.HopDongs.AddRange(newHopDongs);
                    context.SaveChanges();
                }
            }

            // =========================================================================
            // 5. CƠ SỞ VẬT CHẤT & THIẾT BỊ (CoSoVatChats - Đảm bảo >= 180 thiết bị)
            // =========================================================================
            var existingTbIds = context.CoSoVatChats.Select(c => c.MaThietBi).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (existingTbIds.Count < 120)
            {
                var newCsvcs = new List<CoSoVatChat>();
                int tbIdx = 1;
                foreach (var p in allPhongs.Take(45))
                {
                    void AddTb(string ten, string loai, int sl, string tinhTrang)
                    {
                        while (existingTbIds.Contains($"TB{tbIdx:D3}")) tbIdx++;
                        string maTb = $"TB{tbIdx:D3}";
                        tbIdx++;
                        existingTbIds.Add(maTb);
                        newCsvcs.Add(new CoSoVatChat
                        {
                            MaThietBi = maTb,
                            TenThietBi = ten,
                            LoaiThietBi = loai,
                            SoLuong = sl,
                            TinhTrang = tinhTrang,
                            MaPhong = p.MaPhong
                        });
                    }

                    AddTb("Giường tầng sắt sơn tĩnh điện", "Nội thất", p.SoLuongGiuong / 2, "Tốt");
                    AddTb("Tủ quần áo cá nhân", "Nội thất", p.SoLuongGiuong, "Đang sử dụng");
                    AddTb("Bàn học kèm giá sách", "Nội thất", p.SoLuongGiuong, "Tốt");
                    AddTb("Bóng đèn LED Tuýp 1.2m", "Thiết bị điện", 4, "Đang sử dụng");
                    AddTb("Quạt trần Panasonic", "Thiết bị điện", 2, p.MaPhong.EndsWith("03") ? "Cần bảo trì" : "Tốt");
                    AddTb("Bộ vòi hoa sen & Bồn rửa", "Thiết bị vệ sinh", 2, p.MaPhong.EndsWith("07") ? "Hỏng" : "Đang sử dụng");

                    if (p.LoaiPhong.Contains("điều hoà"))
                    {
                        AddTb("Điều hòa Inverter Daikin 12000BTU", "Điện lạnh", 1, p.MaPhong.EndsWith("05") ? "Cần bảo trì" : "Tốt");
                    }
                    if (p.LoaiPhong.Contains("nóng lạnh"))
                    {
                        AddTb("Bình nóng lạnh Ariston 30L", "Điện lạnh", 1, "Tốt");
                    }
                }
                if (newCsvcs.Any())
                {
                    context.CoSoVatChats.AddRange(newCsvcs);
                    context.SaveChanges();
                }
            }

            // =========================================================================
            // 6. HÓA ĐƠN TIỀN PHÒNG & ĐIỆN NƯỚC (HoaDons - Đảm bảo >= 90 hóa đơn)
            // =========================================================================
            var existingHoaDonIds = context.HoaDons.Select(h => h.MaHoaDon).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (existingHoaDonIds.Count < 80)
            {
                var newHoaDons = new List<HoaDon>();
                var svListForBilling = allSinhViens.Where(s => s.TinhTrangLuuTru == "Đang ở" && !string.IsNullOrEmpty(s.MaPhong)).Take(55).ToList();
                string[] phuongThucTT = { "Chuyển khoản", "Ví MoMo", "Tiền mặt (Admin thu)" };

                for (int i = 0; i < svListForBilling.Count; i++)
                {
                    var sv = svListForBilling[i];
                    var phong = allPhongs.FirstOrDefault(p => p.MaPhong == sv.MaPhong);
                    decimal giaPhong = phong?.GiaPhong ?? (sv.MaPhong.StartsWith("1") ? 350000m : 650000m);

                    string maHdPhong = $"HDP_{2026}_{i + 1:D3}";
                    if (!existingHoaDonIds.Contains(maHdPhong))
                    {
                        bool daThuPhong = i % 4 != 0;
                        newHoaDons.Add(new HoaDon
                        {
                            MaHoaDon = maHdPhong,
                            LoaiHoaDon = "Tiền phòng",
                            ChiSoCu = null,
                            ChiSoMoi = null,
                            DonGia = giaPhong, // Đơn giá theo tháng của từng sinh viên
                            TongTien = giaPhong, // Tính đúng theo đầu người của phòng đó
                            HinhThucThanhToan = daThuPhong ? phuongThucTT[i % phuongThucTT.Length] : "Chưa có",
                            TrangThai = daThuPhong ? "Đã thanh toán" : "Chưa thanh toán",
                            NgayLap = DateTime.Now.AddDays(-random.Next(2, 45)),
                            MaSV = sv.MaSV,
                            MaPhong = sv.MaPhong!
                        });
                        existingHoaDonIds.Add(maHdPhong);
                    }

                    string maHdDien = $"HDDN_{2026}_{i + 1:D3}";
                    if (!existingHoaDonIds.Contains(maHdDien))
                    {
                        int csCu = random.Next(120, 450);
                        int csMoi = csCu + random.Next(35, 110);
                        decimal donGia = 2500m; // Đơn giá điện sinh hoạt chuẩn 2.500 VNĐ / kWh
                        bool daThuDien = i % 3 != 0;

                        newHoaDons.Add(new HoaDon
                        {
                            MaHoaDon = maHdDien,
                            LoaiHoaDon = "Điện nước",
                            ChiSoCu = csCu,
                            ChiSoMoi = csMoi,
                            DonGia = donGia,
                            TongTien = (csMoi - csCu) * donGia, // Tính tiền chuẩn: (ChiSoMoi - ChiSoCu) * DonGia
                            HinhThucThanhToan = daThuDien ? phuongThucTT[(i + 1) % phuongThucTT.Length] : "Chưa có",
                            TrangThai = daThuDien ? "Đã thanh toán" : "Chưa thanh toán",
                            NgayLap = DateTime.Now.AddDays(-random.Next(1, 25)),
                            MaSV = sv.MaSV,
                            MaPhong = sv.MaPhong!
                        });
                        existingHoaDonIds.Add(maHdDien);
                    }
                }
                if (newHoaDons.Any())
                {
                    context.HoaDons.AddRange(newHoaDons);
                    context.SaveChanges();
                }

                // Tự động chuẩn hóa hóa đơn cũ nếu CSDL đang lưu đơn giá cũ
                var oldHoaDonsDien = context.HoaDons
                    .Where(h => h.LoaiHoaDon == "Điện nước" && h.DonGia == 3500m && h.ChiSoCu.HasValue && h.ChiSoMoi.HasValue)
                    .ToList();
                if (oldHoaDonsDien.Any())
                {
                    foreach (var h in oldHoaDonsDien)
                    {
                        h.DonGia = 2500m;
                        h.TongTien = (h.ChiSoMoi!.Value - h.ChiSoCu!.Value) * 2500m;
                    }
                    context.SaveChanges();
                }

                var oldHoaDonsPhong = context.HoaDons
                    .Include(h => h.Phong)
                    .Where(h => h.LoaiHoaDon == "Tiền phòng" && h.TongTien > 5000000m)
                    .ToList();
                if (oldHoaDonsPhong.Any())
                {
                    foreach (var h in oldHoaDonsPhong)
                    {
                        decimal rate = h.Phong?.GiaPhong ?? 650000m;
                        h.DonGia = rate;
                        h.TongTien = rate;
                    }
                    context.SaveChanges();
                }
            }

            // =========================================================================
            // 7. BÁO CÁO SỰ CỐ & YÊU CẦU HỖ TRỢ SV (BaoCaoSuCos - Đảm bảo >= 24 bản ghi)
            // =========================================================================
            var existingSuCoIds = context.BaoCaoSuCos.Select(b => b.MaSuCo).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (existingSuCoIds.Count < 20)
            {
                var newSuCos = new List<BaoCaoSuCo>();
                var svSuCoList = allSinhViens.Where(s => s.TinhTrangLuuTru == "Đang ở" && !string.IsNullOrEmpty(s.MaPhong)).Take(25).ToList();

                (string Ten, string MoTa, string MucDo, string TrangThai, bool IsYeuCau)[] mauBaoCao =
                {
                    ("Hỏng quạt trần phòng ở", "Quạt trần quay rất chậm và kêu to, nhờ kỹ thuật kiểm tra tụ quạt.", "Bình thường", "Chờ xử lý", false),
                    ("Điều hòa không mát và chảy nước", "Điều hòa bật 20 độ nhưng chỉ ra gió nóng, nước nhỏ giọt xuống sàn.", "Nghiêm trọng", "Chờ xử lý", false),
                    ("Chập ổ cắm điện bàn học số 3", "Ổ cắm điện có tia lửa khi cắm sạc laptop, cần thợ điện xử lý gấp.", "Khẩn cấp", "Chờ xử lý", false),
                    ("Rò rỉ đường ống nước bồn rửa mặt", "Nước rỉ liên tục dưới gầm chậu rửa mặt gây ướt sàn nhà vệ sinh.", "Nghiêm trọng", "Chờ xử lý", false),
                    ("[Xin chuyển phòng] Nguyện vọng: P.202", "Em muốn xin chuyển sang phòng 202 để ở cùng nhóm bạn học cùng lớp KTPM.", "Bình thường", "Chờ xử lý", true),
                    ("Bóng đèn tuýp hành lang bị cháy", "Đèn ngoài ban công và đèn bàn học số 1 bị nhấp nháy rồi tắt hẳn.", "Bình thường", "Chờ xử lý", false),
                    ("Vòi hoa sen bị gãy khóa nước", "Van gạt nước vòi hoa sen bị gãy không khóa chặt được nước.", "Khẩn cấp", "Đang xử lý", false),
                    ("[Đăng ký gia hạn ở] Nguyện vọng: Học kỳ I 2026-2027", "Em xin đăng ký gia hạn hợp đồng lưu trú tại phòng hiện tại cho kỳ tới.", "Bình thường", "Đang xử lý", true),
                    ("Khóa cửa chính bị kẹt chốt", "Chìa khóa mở rất khó khăn, chốt trong bị lệch.", "Bình thường", "Đang xử lý", false),
                    ("Bình nóng lạnh không lên đèn báo", "Đã bật aptomat bình nóng lạnh 30 phút nhưng nước vẫn lạnh.", "Nghiêm trọng", "Chờ xử lý", false),
                    ("[Đăng ký trả phòng] Nguyện vọng: Cuối tháng này", "Em chuẩn bị đi thực tập doanh nghiệp xa trường nên xin làm thủ tục trả phòng.", "Bình thường", "Chờ xử lý", true),
                    ("Bồn cầu bị tắc thoát nước chậm", "Hệ thống xả nước bồn cầu bị nghẹt, cần thông tắc vệ sinh.", "Nghiêm trọng", "Đã xử lý", false),
                    ("Cửa sổ kính bị lỏng bản lề", "Gió lớn làm cánh cửa sổ va đập mạnh, cần bắt lại vít bản lề.", "Bình thường", "Đã xử lý", false),
                    ("Giường tầng số 2 bị lỏng ốc thang leo", "Thanh sắt bậc thang lên giường tầng 2 bị lung lay.", "Bình thường", "Đã xử lý", false),
                    ("[Xin chuyển phòng] Nguyện vọng: P.305 (Phòng có điều hòa)", "Em muốn nâng cấp từ phòng thường sang phòng có điều hòa tại tầng 3.", "Bình thường", "Đã xử lý", true)
                };

                for (int i = 0; i < svSuCoList.Count; i++)
                {
                    var sv = svSuCoList[i];
                    var mau = mauBaoCao[i % mauBaoCao.Length];
                    string prefix = mau.IsYeuCau ? "YC" : "SC";
                    string maSc = $"{prefix}_2026_{i + 1:D3}";
                    if (existingSuCoIds.Contains(maSc)) continue;

                    newSuCos.Add(new BaoCaoSuCo
                    {
                        MaSuCo = maSc,
                        TenSuCo = mau.Ten,
                        MoTa = mau.MoTa,
                        MucDoKhanCap = mau.MucDo,
                        HinhAnh = "",
                        TrangThai = mau.TrangThai,
                        NgayBaoCao = DateTime.Now.AddHours(-(i * 9 + 2)),
                        MaSV = sv.MaSV,
                        MaPhong = sv.MaPhong!
                    });
                    existingSuCoIds.Add(maSc);
                }
                if (newSuCos.Any())
                {
                    context.BaoCaoSuCos.AddRange(newSuCos);
                    context.SaveChanges();
                }
            }

            // =========================================================================
            // 8. KỶ LUẬT & VI PHẠM NỘI QUY (ViPhams - Đảm bảo >= 35 bản ghi)
            // =========================================================================
            if (context.ViPhams.Count() < 30)
            {
                var newViPhams = new List<ViPham>();
                var svViPhamList = allSinhViens.Skip(5).Take(36).ToList();
                string[] noiDungVp =
                {
                    "Về muộn quá giờ quy định (sau 23:00)",
                    "Sử dụng bếp điện đun nấu trong phòng ở",
                    "Làm ồn, mở nhạc to sau 22:30 đêm",
                    "Dẫn người lạ vào phòng không đăng ký với Bảo vệ",
                    "Không thực hiện trực nhật vệ sinh phòng ở",
                    "Tự ý di chuyển tài sản, giường tủ giữa các phòng"
                };
                string[] hinhThucXl = { "Nhắc nhở", "Cảnh cáo", "Phạt tiền", "Nhắc nhở", "Cảnh cáo", "Đình chỉ" };

                for (int i = 0; i < svViPhamList.Count; i++)
                {
                    var sv = svViPhamList[i];
                    string hinhThuc = hinhThucXl[i % hinhThucXl.Length];
                    newViPhams.Add(new ViPham
                    {
                        MaSV = sv.MaSV,
                        NoiDungViPham = noiDungVp[i % noiDungVp.Length],
                        HinhThucXuLy = hinhThuc,
                        NgayViPham = DateTime.Now.AddDays(-(i * 6 + random.Next(1, 5))),
                        GhiChu = "Đội tự quản và Ban quản lý KTX lập biên bản kiểm tra định kỳ."
                    });
                }
                context.ViPhams.AddRange(newViPhams);
                context.SaveChanges();
            }

            DongBoYeuCauPhongDaDuyet(context);
        }

        /// <summary>
        /// Đồng bộ tự động các yêu cầu hỗ trợ phòng (YC_) đã được Admin duyệt ("Đã xử lý"):
        /// - Nếu là "Đăng ký trả phòng" -> chuyển SinhVien.TinhTrangLuuTru = "Đã rời đi" (sang Lịch sử sinh viên) và mở lại chỗ trống phòng.
        /// - Nếu là "Xin chuyển phòng" -> tự động chuyển SinhVien.MaPhong sang phòng mới (nếu còn chỗ) và đồng bộ trạng thái phòng.
        /// </summary>
        public static void DongBoYeuCauPhongDaDuyet(ApplicationDbContext context)
        {
            var yeuCauDaDuyet = context.BaoCaoSuCos
                .Where(b => b.TrangThai == "Đã xử lý"
                         && (b.MaSuCo.StartsWith("YC_") || b.TenSuCo.StartsWith("["))
                         && !b.MoTa.Contains("[Đã đồng bộ]"))
                .OrderBy(b => b.NgayBaoCao)
                .ToList();

            if (!yeuCauDaDuyet.Any()) return;

            bool hasChanges = false;
            var allRooms = context.Phongs.ToList();

            foreach (var yc in yeuCauDaDuyet)
            {
                var sv = context.SinhViens.FirstOrDefault(s => s.MaSV == yc.MaSV);
                if (sv == null) continue;

                // Đánh dấu yêu cầu này đã được đồng bộ 1 lần duy nhất để không ghi đè khi Admin sửa tay hồ sơ SV về sau
                yc.MoTa = (yc.MoTa ?? "") + " [Đã đồng bộ]";
                context.BaoCaoSuCos.Update(yc);
                hasChanges = true;

                // 1. Duyệt yêu cầu Đăng ký trả phòng -> Chuyển sang "Đã rời đi" (Lịch sử sinh viên)
                if (yc.TenSuCo.Contains("Đăng ký trả phòng", StringComparison.OrdinalIgnoreCase))
                {
                    if (sv.TinhTrangLuuTru != "Đã rời đi")
                    {
                        string? oldRoomCode = sv.MaPhong;
                        sv.TinhTrangLuuTru = "Đã rời đi";
                        context.SinhViens.Update(sv);

                        if (!string.IsNullOrEmpty(oldRoomCode))
                        {
                            var oldRoom = allRooms.FirstOrDefault(p => p.MaPhong == oldRoomCode);
                            if (oldRoom != null && oldRoom.TinhTrang == "Đã đầy")
                            {
                                oldRoom.TinhTrang = "Còn chỗ";
                                context.Phongs.Update(oldRoom);
                            }
                        }
                    }
                }
                // 2. Duyệt yêu cầu Xin chuyển phòng -> Cập nhật sang mã phòng mới
                else if (yc.TenSuCo.Contains("Xin chuyển phòng", StringComparison.OrdinalIgnoreCase))
                {
                    string rawTarget = "";
                    int idxArrow = yc.TenSuCo.LastIndexOf("phòng", StringComparison.OrdinalIgnoreCase);
                    if (yc.TenSuCo.Contains("->", StringComparison.Ordinal) && idxArrow >= 0)
                    {
                        rawTarget = yc.TenSuCo.Substring(idxArrow + "phòng".Length).Trim();
                    }
                    else
                    {
                        int idxNv = yc.TenSuCo.IndexOf("Nguyện vọng:", StringComparison.OrdinalIgnoreCase);
                        if (idxNv >= 0)
                        {
                            rawTarget = yc.TenSuCo.Substring(idxNv + "Nguyện vọng:".Length).Trim();
                        }
                    }

                    int parenIdx = rawTarget.IndexOf('(');
                    if (parenIdx > 0) rawTarget = rawTarget.Substring(0, parenIdx).Trim();

                    rawTarget = rawTarget.ToUpper();
                    if (rawTarget.StartsWith("P.")) rawTarget = rawTarget.Substring(2).Trim();
                    else if (rawTarget.StartsWith("P ")) rawTarget = rawTarget.Substring(2).Trim();
                    string normTarget = rawTarget.Replace(".", "").Replace(" ", "").Replace("-", "");

                    if (!string.IsNullOrEmpty(normTarget))
                    {
                        var targetRoom = allRooms.FirstOrDefault(p =>
                            p.MaPhong.Equals(rawTarget, StringComparison.OrdinalIgnoreCase) ||
                            p.MaPhong.Replace(".", "").Replace(" ", "").Replace("-", "").ToUpper() == normTarget);

                        if (targetRoom != null && sv.MaPhong != targetRoom.MaPhong)
                        {
                            int soDangO = context.SinhViens.Count(s => s.MaPhong == targetRoom.MaPhong && s.TinhTrangLuuTru == "Đang ở" && s.MaSV != sv.MaSV);
                            if (soDangO < targetRoom.SoLuongGiuong)
                            {
                                string? oldRoomCode = sv.MaPhong;
                                sv.MaPhong = targetRoom.MaPhong;
                                context.SinhViens.Update(sv);

                                if (soDangO + 1 >= targetRoom.SoLuongGiuong && targetRoom.TinhTrang == "Còn chỗ")
                                {
                                    targetRoom.TinhTrang = "Đã đầy";
                                    context.Phongs.Update(targetRoom);
                                }
                                if (!string.IsNullOrEmpty(oldRoomCode))
                                {
                                    var oldRoom = allRooms.FirstOrDefault(p => p.MaPhong == oldRoomCode);
                                    if (oldRoom != null && oldRoom.TinhTrang == "Đã đầy")
                                    {
                                        oldRoom.TinhTrang = "Còn chỗ";
                                        context.Phongs.Update(oldRoom);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (hasChanges)
            {
                context.SaveChanges();
            }
        }
    }
}
