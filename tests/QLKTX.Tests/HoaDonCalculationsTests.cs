using System;
using Xunit;
using QLKTX.Models;

namespace QLKTX.Tests
{
    /// <summary>
    /// Bộ kiểm thử đơn vị: Nghiệp vụ tính phí, kiểm tra chỉ số điện nước và hóa đơn tiền phòng.
    /// Đáp ứng tiêu chí bảo mật tài chính: Nghiêm cấm hóa đơn âm và tính toán chính xác 100%.
    /// </summary>
    public class HoaDonCalculationsTests
    {
        [Theory]
        [InlineData(100, 150, 2500, 125000)]
        [InlineData(200, 320, 2500, 300000)]
        [InlineData(0, 80, 2500, 200000)]
        [InlineData(50, 100, 3000, 150000)]
        public void TinhTienDienNuoc_HopLe_TraVeKetQuaChinhXac(int chiSoCu, int chiSoMoi, decimal donGia, decimal expectedTongTien)
        {
            // Act
            decimal tongTien = HoaDonCalculator.TinhTienDienNuoc(chiSoCu, chiSoMoi, donGia);

            // Assert
            Assert.Equal(expectedTongTien, tongTien);
        }

        [Theory]
        [InlineData(150, 100)] // ChiSoMoi < ChiSoCu (chỉ số quay lùi)
        [InlineData(150, 150)] // ChiSoMoi == ChiSoCu (chỉ số bằng nhau)
        [InlineData(500, 0)]   // ChiSoMoi nhỏ hơn nhiều
        public void TinhTienDienNuoc_ChiSoMoiNhoHonHoacBangChiSoCu_NemNgoaiLe(int chiSoCu, int chiSoMoi)
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                HoaDonCalculator.TinhTienDienNuoc(chiSoCu, chiSoMoi, 2500m));

            Assert.Contains("lớn hơn", ex.Message);
        }

        [Theory]
        [InlineData(100, 150, true)]
        [InlineData(150, 150, false)]
        [InlineData(200, 180, false)]
        [InlineData(null, 150, false)]
        [InlineData(100, null, false)]
        [InlineData(null, null, false)]
        public void KiemTraChiSoHopLe_XacThucDieuKienBienChinhXac(int? chiSoCu, int? chiSoMoi, bool expectedHopLe)
        {
            // Act
            bool hopLe = HoaDonCalculator.KiemTraChiSoHopLe(chiSoCu, chiSoMoi, out string? errorMessage);

            // Assert
            Assert.Equal(expectedHopLe, hopLe);
            if (!expectedHopLe)
            {
                Assert.NotNull(errorMessage);
            }
            else
            {
                Assert.Null(errorMessage);
            }
        }

        [Theory]
        [InlineData("Phòng tiêu chuẩn", 1, 650000)]
        [InlineData("Phòng tiêu chuẩn", 5, 3250000)]
        [InlineData("Phòng Chất lượng cao", 1, 1200000)]
        [InlineData("Phòng Chất lượng cao (2 giường)", 6, 7200000)]
        [InlineData("Phòng VIP", 10, 12000000)]
        public void TinhTienPhong_TheoLoaiPhongVaSoThang_KetQuaChinhXac(string loaiPhong, int soThang, decimal expectedTienPhong)
        {
            // Act
            decimal tienPhong = HoaDonCalculator.TinhTienPhong(loaiPhong, soThang);

            // Assert
            Assert.Equal(expectedTienPhong, tienPhong);
        }

        [Fact]
        public void TinhTienPhong_SoThangKhongHopLe_NemNgoaiLe()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                HoaDonCalculator.TinhTienPhong("Phòng tiêu chuẩn", 0));

            Assert.Throws<ArgumentException>(() =>
                HoaDonCalculator.TinhTienPhong("Phòng tiêu chuẩn", -3));
        }

        [Fact]
        public void TinhTienPhong_CoDonGiaThoaThuan_UuTienDonGiaThoaThuan()
        {
            // Arrange
            decimal donGiaThoaThuan = 800000m;
            int soThang = 3;

            // Act
            decimal tienPhong = HoaDonCalculator.TinhTienPhong("Phòng tiêu chuẩn", soThang, donGiaThoaThuan);

            // Assert
            Assert.Equal(2400000m, tienPhong);
        }
    }
}

