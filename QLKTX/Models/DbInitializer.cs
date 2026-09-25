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
            // Kiểm tra xem database đã có Phòng nào chưa. Nếu có rồi thì dừng để tránh tạo trùng lặp.
            if (context.Phongs.Any())
            {
                return;
            }
            // Ra lệnh xóa sạch dữ liệu Sinh Viên và Phòng cũ đi
            //if (context.SinhViens.Any())
            //{
            //    context.SinhViens.RemoveRange(context.SinhViens);
            //    context.SaveChanges();
            //}
            //if (context.Phongs.Any())
            //{
            //    context.Phongs.RemoveRange(context.Phongs);
            //    context.SaveChanges();
            //}

            var random = new Random();

            
            // 1. TẠO DANH SÁCH PHÒNG THEO YÊU CẦU
            
            var phongs = new List<Phong>();
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
            
            var sinhViens = new List<SinhVien>();
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
    }
}