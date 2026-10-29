using System;

namespace QLKTX.Models
{
    /// <summary>
    /// Bộ tính toán và xác thực nghiệp vụ Hóa đơn (Điện nước, Tiền phòng, Công nợ).
    /// Đảm bảo tính nhất quán nghiệp vụ, ngăn chặn hóa đơn âm và tính tiền chuẩn theo loại phòng.
    /// </summary>
    public static class HoaDonCalculator
    {
        public const decimal DonGiaDienMacDinh = 2500m; // 2.500 VNĐ / kWh
        public const decimal GiaPhongTieuChuan = 650000m; // 650.000 VNĐ / tháng
        public const decimal GiaPhongChatLuongCao = 1200000m; // 1.200.000 VNĐ / tháng

        /// <summary>
        /// Tính tiền điện nước theo công thức: (ChiSoMoi - ChiSoCu) * DonGia
        /// Ném ngoại lệ ArgumentException nếu ChiSoMoi <= ChiSoCu để bảo vệ tính toàn vẹn tài chính.
        /// </summary>
        public static decimal TinhTienDienNuoc(int chiSoCu, int chiSoMoi, decimal donGia = DonGiaDienMacDinh)
        {
            if (chiSoMoi <= chiSoCu)
            {
                throw new ArgumentException("Chỉ số mới phải lớn hơn chỉ số cũ để tránh phát sinh hóa đơn âm.");
            }

            if (donGia <= 0)
            {
                throw new ArgumentException("Đơn giá điện nước phải lớn hơn 0.");
            }

            return (chiSoMoi - chiSoCu) * donGia;
        }

        /// <summary>
        /// Xác thực điều kiện biên của chỉ số điện nước trước khi lập hóa đơn.
        /// </summary>
        public static bool KiemTraChiSoHopLe(int? chiSoCu, int? chiSoMoi, out string? errorMessage)
        {
            errorMessage = null;

            if (!chiSoCu.HasValue || !chiSoMoi.HasValue)
            {
                errorMessage = "Vui lòng nhập đầy đủ Chỉ số cũ và Chỉ số mới.";
                return false;
            }

            if (chiSoMoi.Value <= chiSoCu.Value)
            {
                errorMessage = "Chỉ số mới phải lớn hơn Chỉ số cũ (ngăn chặn hóa đơn âm).";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Tính tiền phòng theo loại phòng và số tháng thuê.
        /// </summary>
        public static decimal TinhTienPhong(string loaiPhong, int soThang = 1, decimal? donGiaThang = null)
        {
            if (soThang <= 0)
            {
                throw new ArgumentException("Số tháng thuê phòng phải lớn hơn 0.");
            }

            decimal giaTheoThang;
            if (donGiaThang.HasValue && donGiaThang.Value > 0)
            {
                giaTheoThang = donGiaThang.Value;
            }
            else if (!string.IsNullOrWhiteSpace(loaiPhong) &&
                    (loaiPhong.Contains("Chất lượng cao", StringComparison.OrdinalIgnoreCase) ||
                     loaiPhong.Contains("VIP", StringComparison.OrdinalIgnoreCase) ||
                     loaiPhong.Contains("2 giường", StringComparison.OrdinalIgnoreCase)))
            {
                giaTheoThang = GiaPhongChatLuongCao;
            }
            else
            {
                giaTheoThang = GiaPhongTieuChuan;
            }

            return giaTheoThang * soThang;
        }
    }
}

