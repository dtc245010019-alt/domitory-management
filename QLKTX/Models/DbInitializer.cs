using QLKTX.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QLKTX // Hoặc namespace tương ứng với project của em
{
    public static class DbInitializer
    {
        public static void Initialize(ApplicationDbContext context)
        {
            var random = new Random();
            var phongs = new List<Phong>();
            var sinhViens = new List<SinhVien>();

            if (!context.Phongs.Any())
            {
                // 1. TẠO DANH SÁCH PHÒNG THEO YÊU CẦU
            string[] toaNha = { "A1", "A2", "B1", "B2", "C1", "C2" };

            foreach (var toa in toaNha)
            {
                for (int i = 1; i <= 15; i++) // Mỗi tòa tạo 15 phòng
                {
                    string maPhong = $"{toa}-{i:D2}"; // VD: A1-01, A1-02

                    // Random 3 loại phòng và giá tương ứng
                    int loai = random.Next(1, 4); // Random 1, 2, hoặc 3
                    string loaiPhong = loai == 1 ? "Phòng thường" : (loai == 2 ? "Phòng có điều hoà" : "Phòng điều hoà + nóng lạnh");
                    decimal giaPhong = loai == 1 ? 9000000m : (loai == 2 ? 11000000m : 13000000m);

                    // Trộn tỷ lệ số giường: Đa số là 4-5, một số ít là 3 và 6
                    int[] tiLeGiuong = { 3, 4, 4, 4, 5, 5, 5, 6 };
                    int soGiuong = tiLeGiuong[random.Next(tiLeGiuong.Length)];

                    phongs.Add(new Phong
                    {
                        MaPhong = maPhong,
                        LoaiPhong = loaiPhong,
                        SoLuongGiuong = soGiuong,
                        GiaPhong = giaPhong,
                        TinhTrang = "Còn chỗ"
                    });
                }
            }
            context.Phongs.AddRange(phongs);
            context.SaveChanges();

            
            // 2. TẠO HÀNG TRĂM SINH VIÊN (DTC001 - DTC999)
            
            string[] ho = { "Nguyễn", "Trần", "Lê", "Phạm", "Hoàng", "Huỳnh", "Vũ", "Võ", "Đặng", "Bùi", "Đỗ", "Hồ", "Ngô", "Dương", "Lý" };
            string[] dem = { "Văn", "Thị", "Hữu", "Đức", "Ngọc", "Thanh", "Minh", "Thu", "Hải", "Xuân", "Gia", "Bảo" };
            string[] ten = { "An", "Anh", "Bảo", "Chi", "Dũng", "Duy", "Đạt", "Giang", "Hà", "Hải", "Hiếu", "Hùng", "Hương", "Khang", "Khánh", "Khoa", "Kiên", "Lâm", "Lan", "Linh", "Long", "Mai", "Minh", "Nam", "Nga", "Ngọc", "Nhi", "Phúc", "Phương", "Quân", "Quang", "Sơn", "Tâm", "Thảo", "Thắng", "Thành", "Trang", "Trung", "Tuấn", "Uyên", "Vân", "Việt", "Vinh", "Yến" };

            int svIdCounter = 1;

            foreach (var p in phongs)
            {
                // Xếp ngẫu nhiên sinh viên vào phòng (có thể đầy, hoặc trống 1-2 giường)
                int soSinhVienHienTai = random.Next(p.SoLuongGiuong - 2, p.SoLuongGiuong + 1);
                if (soSinhVienHienTai < 0) soSinhVienHienTai = 0;

                for (int i = 0; i < soSinhVienHienTai; i++)
                {
                    if (svIdCounter > 999) break; // Giới hạn đến DTC999

                    // Mã SV: DTC001, DTC002...
                    string maSV = $"DTC{svIdCounter:D3}";
                    svIdCounter++;

                    // Tuổi 18-23 -> Sinh từ năm 2003 đến 2008 (tính từ mốc 2026)
                    int namSinh = random.Next(2003, 2009);
                    DateTime ngaySinh = new DateTime(namSinh, random.Next(1, 13), random.Next(1, 28));

                    // Sinh CCCD định dạng 0272xxxxxxxx
                    string cccd = $"0272{random.Next(10000000, 99999999)}";

                    // SĐT ngẫu nhiên
                    string sdt = $"0{random.Next(3, 9)}{random.Next(10000000, 99999999)}";

                    sinhViens.Add(new SinhVien
                    {
                        MaSV = maSV,
                        HoTen = $"{ho[random.Next(ho.Length)]} {dem[random.Next(dem.Length)]} {ten[random.Next(ten.Length)]}",
                        NgaySinh = ngaySinh,
                        Lop = $"K{random.Next(20, 24)}IT{random.Next(1, 6)}",
                        SoDienThoai = sdt,
                        CCCD = cccd,
                        TinhTrangLuuTru = "Đang ở",
                        MaPhong = p.MaPhong
                    });
                }

                // Cập nhật lại trạng thái phòng nếu đã kín giường
                if (soSinhVienHienTai == p.SoLuongGiuong)
                {
                    p.TinhTrang = "Đã đầy";
                }
            }
            context.SinhViens.AddRange(sinhViens);
            context.SaveChanges();
            // 3. TẠO TÀI KHOẢN ĐĂNG NHẬP MẶC ĐỊNH
if (!context.TaiKhoans.Any())
{
    var listTK = new List<TaiKhoan>
    {
        // Tài khoản Admin
        new TaiKhoan { TenDangNhap = "admin", MatKhau = "123", Quyen ="admin", MaSV = null },
        // Tài khoản Sinh viên DTC001
        new TaiKhoan { TenDangNhap = "DTC001", MatKhau = "123", Quyen = "sinhvien", MaSV = "DTC001" }
    };
    context.TaiKhoans.AddRange(listTK);
    context.SaveChanges();
}
            }
            else
            {
                phongs = context.Phongs.ToList();
                sinhViens = context.SinhViens.ToList();
            }

            // ==========================================
            // SEED DATA CHO CÁC BẢNG CÒN LẠI (YÊU CẦU MỚI)
            // ==========================================

            // 4. LỊCH SỬ SINH VIÊN ĐÃ RỜI ĐI
            if (!context.SinhViens.Any(s => s.TinhTrangLuuTru == "Đã rời đi"))
            {
                var svDaRoi = sinhViens.OrderBy(s => random.Next()).Take(20).ToList();
                foreach (var sv in svDaRoi)
                {
                    sv.TinhTrangLuuTru = "Đã rời đi";
                }
                context.SaveChanges();
            }

            // 5. HỢP ĐỒNG (HopDong)
            if (!context.HopDongs.Any())
            {
                var hopDongs = new List<HopDong>();
                var svHopDong = sinhViens.Take(20).ToList();
                for (int i = 0; i < svHopDong.Count; i++)
                {
                    var sv = svHopDong[i];
                    hopDongs.Add(new HopDong
                    {
                        MaHopDong = $"HD{DateTime.Now.Ticks.ToString().Substring(8)}_{i}",
                        NgayBatDau = new DateTime(2023, 8, 15),
                        NgayKetThuc = new DateTime(2024, 6, 30),
                        TienDatCoc = 1500000m,
                        TrangThai = i % 4 == 0 ? "Hết hiệu lực" : "Còn hiệu lực",
                        DieuKhoan = "Tuân thủ nội quy KTX, đóng tiền đúng hạn.",
                        MaSV = sv.MaSV,
                        MaPhong = sv.MaPhong
                    });
                }
                context.HopDongs.AddRange(hopDongs);
                context.SaveChanges();
            }

            // 6. CƠ SỞ VẬT CHẤT (CoSoVatChat)
            if (!context.CoSoVatChats.Any())
            {
                var csvcs = new List<CoSoVatChat>();
                int idCounter = 1;
                // Seed cơ sở vật chất cho 10 phòng đầu tiên
                foreach (var p in phongs.Take(10))
                {
                    csvcs.Add(new CoSoVatChat { MaThietBi = $"TB{idCounter++:D3}", TenThietBi = "Giường tầng", LoaiThietBi = "Nội thất", SoLuong = p.SoLuongGiuong / 2, TinhTrang = "Tốt", MaPhong = p.MaPhong });
                    csvcs.Add(new CoSoVatChat { MaThietBi = $"TB{idCounter++:D3}", TenThietBi = "Tủ quần áo", LoaiThietBi = "Nội thất", SoLuong = p.SoLuongGiuong, TinhTrang = "Đang sử dụng", MaPhong = p.MaPhong });
                    csvcs.Add(new CoSoVatChat { MaThietBi = $"TB{idCounter++:D3}", TenThietBi = "Quạt trần", LoaiThietBi = "Điện tử", SoLuong = 2, TinhTrang = p.MaPhong.Contains("01") ? "Cần bảo trì" : "Tốt", MaPhong = p.MaPhong });
                    
                    if (p.LoaiPhong.Contains("điều hoà"))
                    {
                        csvcs.Add(new CoSoVatChat { MaThietBi = $"TB{idCounter++:D3}", TenThietBi = "Điều hòa Daikin", LoaiThietBi = "Điện tử", SoLuong = 1, TinhTrang = "Tốt", MaPhong = p.MaPhong });
                    }
                }
                context.CoSoVatChats.AddRange(csvcs);
                context.SaveChanges();
            }

            // 7. VI PHẠM (ViPham)
            if (!context.ViPhams.Any())
            {
                var viPhams = new List<ViPham>();
                var svViPham = sinhViens.Skip(20).Take(10).ToList();
                string[] lyDo = { "Về khuya quá giờ quy định", "Đun nấu trong phòng", "Gây ồn ào mất trật tự", "Không trực nhật vệ sinh phòng" };
                string[] hinhThuc = { "Nhắc nhở", "Cảnh cáo", "Phạt tiền 100k", "Đình chỉ nội trú" };
                
                for (int i = 0; i < svViPham.Count; i++)
                {
                    viPhams.Add(new ViPham
                    {
                        // MaViPham là auto-increment nên không cần set
                        NoiDungViPham = lyDo[i % lyDo.Length],
                        HinhThucXuLy = hinhThuc[i % hinhThuc.Length],
                        NgayViPham = DateTime.Now.AddDays(-random.Next(1, 60)),
                        GhiChu = "Đã thông báo cho ban quản lý",
                        MaSV = svViPham[i].MaSV
                    });
                }
                context.ViPhams.AddRange(viPhams);
                context.SaveChanges();
            }

            // 8. HÓA ĐƠN (HoaDon)
            if (!context.HoaDons.Any())
            {
                var hoaDons = new List<HoaDon>();
                var svHoaDon = sinhViens.Take(30).ToList();
                for (int i = 0; i < svHoaDon.Count; i++)
                {
                    var sv = svHoaDon[i];
                    
                    // Tạo hóa đơn Tiền phòng
                    hoaDons.Add(new HoaDon
                    {
                        MaHoaDon = $"HD_{DateTime.Now.Ticks.ToString().Substring(8)}_P{i}",
                        LoaiHoaDon = "Tiền phòng",
                        ChiSoCu = null,
                        ChiSoMoi = null,
                        DonGia = 1200000m,
                        TongTien = 1200000m,
                        HinhThucThanhToan = "Chuyển khoản",
                        TrangThai = i % 4 == 0 ? "Chưa thanh toán" : "Đã thanh toán",
                        NgayLap = DateTime.Now.AddDays(-random.Next(1, 30)),
                        MaSV = sv.MaSV,
                        MaPhong = sv.MaPhong
                    });

                    // Tạo hóa đơn Điện nước
                    int chiSoCu = random.Next(100, 500);
                    int chiSoMoi = chiSoCu + random.Next(30, 100);
                    decimal donGiaDN = 3500m; // 3500 VNĐ / số điện
                    decimal tongTienDN = (chiSoMoi - chiSoCu) * donGiaDN;
                    
                    hoaDons.Add(new HoaDon
                    {
                        MaHoaDon = $"HD_{DateTime.Now.Ticks.ToString().Substring(8)}_DN{i}",
                        LoaiHoaDon = "Điện nước",
                        ChiSoCu = chiSoCu,
                        ChiSoMoi = chiSoMoi,
                        DonGia = donGiaDN,
                        TongTien = tongTienDN,
                        HinhThucThanhToan = i % 2 == 0 ? "Tiền mặt" : "Chuyển khoản",
                        TrangThai = i % 3 == 0 ? "Chưa thanh toán" : "Đã thanh toán",
                        NgayLap = DateTime.Now.AddDays(-random.Next(1, 30)),
                        MaSV = sv.MaSV,
                        MaPhong = sv.MaPhong
                    });
                }
                context.HoaDons.AddRange(hoaDons);
                context.SaveChanges();
            }

            // 9. BÁO CÁO SỰ CỐ (BaoCaoSuCo)
            if (!context.BaoCaoSuCos.Any())
            {
                var suCos = new List<BaoCaoSuCo>();
                var svSuCo = sinhViens.Skip(30).Take(15).ToList();
                string[] tenSuCo = { "Hỏng quạt trần", "Điều hòa không mát", "Bóng đèn bị cháy", "Nước chảy yếu", "Cửa phòng hỏng khóa" };
                string[] mucDo = { "Thấp", "Bình thường", "Khẩn cấp" };
                string[] trangThai = { "Chờ xử lý", "Đang xử lý", "Đã xử lý" };
                
                for (int i = 0; i < svSuCo.Count; i++)
                {
                    var sv = svSuCo[i];
                    suCos.Add(new BaoCaoSuCo
                    {
                        MaSuCo = $"SC_{DateTime.Now.Ticks.ToString().Substring(8)}_{i}",
                        TenSuCo = tenSuCo[i % tenSuCo.Length],
                        MoTa = "Thiết bị hỏng đột ngột, mong ban quản lý cử người xuống sửa chữa sớm.",
                        MucDoKhanCap = mucDo[i % mucDo.Length],
                        HinhAnh = null,
                        TrangThai = trangThai[i % trangThai.Length],
                        NgayBaoCao = DateTime.Now.AddDays(-random.Next(1, 15)),
                        MaSV = sv.MaSV,
                        MaPhong = sv.MaPhong
                    });
                }
                context.BaoCaoSuCos.AddRange(suCos);
                context.SaveChanges();
            }
        }
    }
}