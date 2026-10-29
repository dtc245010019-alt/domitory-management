using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using QLKTX.Models;

namespace QLKTX.Tests
{
    /// <summary>
    /// Bộ kiểm thử đơn vị: Nghiệp vụ Quản lý Phòng và Đồng bộ Sĩ số X/Y.
    /// Tuân thủ các nguyên tắc nghiệp vụ bất khả xâm phạm:
    /// 1. Sĩ số thực tế chỉ đếm sinh viên có trạng thái "Đang ở".
    /// 2. Xóa mềm (Soft Delete) không làm mất dữ liệu và tự động giải phóng phòng.
    /// 3. Đình chỉ kỷ luật tự động giải phóng giường trống.
    /// </summary>
    public class PhongVaSiSoTests
    {
        private ApplicationDbContext CreateInMemoryDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            return new ApplicationDbContext(options);
        }

        private async Task DongBoTrangThaiPhongAsync(ApplicationDbContext context, string maPhong)
        {
            var phong = await context.Phongs.FirstOrDefaultAsync(p => p.MaPhong == maPhong);
            if (phong == null || phong.TinhTrang == "Bảo trì" || phong.TinhTrang == "Đang sửa chữa") return;

            // Đếm chỉ những sinh viên Đang ở (Bỏ qua "Đã rời đi", "Đình chỉ")
            int soDangO = await context.SinhViens.CountAsync(s => s.MaPhong == maPhong && s.TinhTrangLuuTru == "Đang ở");
            phong.TinhTrang = (soDangO >= phong.SoLuongGiuong) ? "Đã đầy" : "Còn chỗ";
            context.Phongs.Update(phong);
            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task XepSinhVien_VaoPhongConCho_TangSiSoVaGiuNguyenTrangThaiConCho()
        {
            // Arrange
            using var context = CreateInMemoryDbContext("Test_XepPhong_ConCho");
            var phong = new Phong
            {
                MaPhong = "A101",
                LoaiPhong = "Phòng tiêu chuẩn",
                SoLuongGiuong = 4,
                GiaPhong = 650000m,
                TinhTrang = "Còn chỗ"
            };
            context.Phongs.Add(phong);

            // Đã có 1 sinh viên đang ở (sĩ số 1/4)
            var sv1 = new SinhVien
            {
                MaSV = "SV001",
                HoTen = "Nguyễn Văn A",
                NgaySinh = new DateTime(2003, 1, 1),
                Lop = "CNTT K20",
                SoDienThoai = "0981234567",
                MaPhong = "A101",
                TinhTrangLuuTru = "Đang ở"
            };
            context.SinhViens.Add(sv1);
            await context.SaveChangesAsync();

            // Act: Xếp thêm sinh viên mới thứ 2 vào phòng
            var sv2 = new SinhVien
            {
                MaSV = "SV002",
                HoTen = "Trần Thị B",
                NgaySinh = new DateTime(2003, 5, 10),
                Lop = "CNTT K20",
                SoDienThoai = "0912345678",
                MaPhong = "A101",
                TinhTrangLuuTru = "Đang ở"
            };
            context.SinhViens.Add(sv2);
            await context.SaveChangesAsync();
            await DongBoTrangThaiPhongAsync(context, "A101");

            // Assert
            int siSoThucTe = await context.SinhViens.CountAsync(s => s.MaPhong == "A101" && s.TinhTrangLuuTru == "Đang ở");
            var phongCapNhat = await context.Phongs.FindAsync("A101");

            Assert.Equal(2, siSoThucTe);
            Assert.NotNull(phongCapNhat);
            Assert.Equal("Còn chỗ", phongCapNhat.TinhTrang);
        }

        [Fact]
        public async Task XepSinhVien_VaoGiuongCuoiCung_TuDongChuyenSangDaDay()
        {
            // Arrange
            using var context = CreateInMemoryDbContext("Test_XepPhong_GiuongCuoiCung");
            var phong = new Phong
            {
                MaPhong = "B201",
                LoaiPhong = "Phòng 2 giường VIP",
                SoLuongGiuong = 2,
                GiaPhong = 1200000m,
                TinhTrang = "Còn chỗ"
            };
            context.Phongs.Add(phong);

            var sv1 = new SinhVien
            {
                MaSV = "SV101",
                HoTen = "Lê Văn C",
                NgaySinh = new DateTime(2002, 3, 15),
                Lop = "KTPM K19",
                SoDienThoai = "0977112233",
                MaPhong = "B201",
                TinhTrangLuuTru = "Đang ở"
            };
            context.SinhViens.Add(sv1);
            await context.SaveChangesAsync();

            // Act: Xếp sinh viên thứ 2 (lấp đầy giường cuối cùng)
            var sv2 = new SinhVien
            {
                MaSV = "SV102",
                HoTen = "Phạm Văn D",
                NgaySinh = new DateTime(2002, 8, 20),
                Lop = "KTPM K19",
                SoDienThoai = "0988223344",
                MaPhong = "B201",
                TinhTrangLuuTru = "Đang ở"
            };
            context.SinhViens.Add(sv2);
            await context.SaveChangesAsync();
            await DongBoTrangThaiPhongAsync(context, "B201");

            // Assert
            int siSo = await context.SinhViens.CountAsync(s => s.MaPhong == "B201" && s.TinhTrangLuuTru == "Đang ở");
            var phongCapNhat = await context.Phongs.FindAsync("B201");

            Assert.Equal(2, siSo);
            Assert.NotNull(phongCapNhat);
            Assert.Equal("Đã đầy", phongCapNhat.TinhTrang);
        }

        [Fact]
        public async Task XoaMemSinhVien_ChuyenDaRoiDi_GiamSiSoVaMoLaiConCho()
        {
            // Arrange: Phòng 2 giường đã đầy (2/2)
            using var context = CreateInMemoryDbContext("Test_SoftDelete_GiaiPhongPhong");
            var phong = new Phong
            {
                MaPhong = "C301",
                LoaiPhong = "Phòng tiêu chuẩn",
                SoLuongGiuong = 2,
                GiaPhong = 650000m,
                TinhTrang = "Đã đầy"
            };
            context.Phongs.Add(phong);

            var sv1 = new SinhVien
            {
                MaSV = "SV201",
                HoTen = "Hoàng Văn E",
                NgaySinh = new DateTime(2003, 2, 2),
                Lop = "DTVT K20",
                SoDienThoai = "0911223344",
                MaPhong = "C301",
                TinhTrangLuuTru = "Đang ở"
            };
            var sv2 = new SinhVien
            {
                MaSV = "SV202",
                HoTen = "Vũ Thị F",
                NgaySinh = new DateTime(2003, 4, 4),
                Lop = "DTVT K20",
                SoDienThoai = "0922334455",
                MaPhong = "C301",
                TinhTrangLuuTru = "Đang ở"
            };
            context.SinhViens.AddRange(sv1, sv2);
            await context.SaveChangesAsync();

            // Act: Sinh viên SV201 trả phòng (Xóa mềm -> TinhTrangLuuTru = "Đã rời đi")
            sv1.TinhTrangLuuTru = "Đã rời đi";
            context.SinhViens.Update(sv1);
            await context.SaveChangesAsync();
            await DongBoTrangThaiPhongAsync(context, "C301");

            // Assert: Bản ghi vẫn tồn tại trong CSDL, sĩ số giảm về 1/2 và phòng mở lại "Còn chỗ"
            var svSauXoa = await context.SinhViens.FindAsync("SV201");
            Assert.NotNull(svSauXoa);
            Assert.Equal("Đã rời đi", svSauXoa.TinhTrangLuuTru);

            int siSoThucTe = await context.SinhViens.CountAsync(s => s.MaPhong == "C301" && s.TinhTrangLuuTru == "Đang ở");
            var phongCapNhat = await context.Phongs.FindAsync("C301");

            Assert.Equal(1, siSoThucTe);
            Assert.NotNull(phongCapNhat);
            Assert.Equal("Còn chỗ", phongCapNhat.TinhTrang);
        }

        [Fact]
        public async Task DinhChiKyLuatSinhVien_TuDongGiaiPhongGiuongTrong()
        {
            // Arrange: Phòng 1 giường đang đầy (1/1)
            using var context = CreateInMemoryDbContext("Test_DinhChi_GiaiPhongGiuong");
            var phong = new Phong
            {
                MaPhong = "D401",
                LoaiPhong = "Phòng đơn",
                SoLuongGiuong = 1,
                GiaPhong = 1500000m,
                TinhTrang = "Đã đầy"
            };
            context.Phongs.Add(phong);

            var sv = new SinhVien
            {
                MaSV = "SV301",
                HoTen = "Đỗ Văn G",
                NgaySinh = new DateTime(2001, 7, 7),
                Lop = "HTTT K18",
                SoDienThoai = "0933445566",
                MaPhong = "D401",
                TinhTrangLuuTru = "Đang ở"
            };
            context.SinhViens.Add(sv);
            await context.SaveChangesAsync();

            // Act: Lập biên bản vi phạm kỷ luật mức "Đình chỉ lưu trú"
            var viPham = new ViPham
            {
                MaSV = "SV301",
                NgayViPham = DateTime.Now,
                NoiDungViPham = "Vi phạm nghiêm trọng quy chế an toàn KTX",
                HinhThucXuLy = "Đình chỉ",
                GhiChu = "Đình chỉ lưu trú học kỳ 1"
            };
            context.ViPhams.Add(viPham);

            // Đồng bộ trạng thái sinh viên bị đình chỉ và giải phóng phòng
            sv.TinhTrangLuuTru = "Đình chỉ";
            context.SinhViens.Update(sv);
            await context.SaveChangesAsync();
            await DongBoTrangThaiPhongAsync(context, "D401");

            // Assert
            var svCapNhat = await context.SinhViens.FindAsync("SV301");
            Assert.NotNull(svCapNhat);
            Assert.Equal("Đình chỉ", svCapNhat.TinhTrangLuuTru);

            int siSoThucTe = await context.SinhViens.CountAsync(s => s.MaPhong == "D401" && s.TinhTrangLuuTru == "Đang ở");
            var phongCapNhat = await context.Phongs.FindAsync("D401");

            Assert.Equal(0, siSoThucTe);
            Assert.NotNull(phongCapNhat);
            Assert.Equal("Còn chỗ", phongCapNhat.TinhTrang);
        }
    }
}

