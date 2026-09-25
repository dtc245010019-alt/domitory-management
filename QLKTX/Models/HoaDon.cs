using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QLKTX.Models
{
    public class HoaDon
    {
        [Key]
        [StringLength(20)]
        public string MaHoaDon { get; set; }

        [Required]
        [StringLength(50)]
        public string LoaiHoaDon { get; set; } // "Tiền phòng" hoặc "Điện nước" 

        public int? ChiSoCu { get; set; } // Cho phép null nếu là tiền phòng 
        public int? ChiSoMoi { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DonGia { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TongTien { get; set; }

        [StringLength(50)]
        public string HinhThucThanhToan { get; set; } // Tiền mặt, Chuyển khoản 

        [StringLength(50)]
        public string TrangThai { get; set; } = "Chưa thanh toán"; // Đã thanh toán, Chưa thanh toán 

        public DateTime NgayLap { get; set; } = DateTime.Now;

        // Liên kết
        public string MaSV { get; set; }
        [ForeignKey("MaSV")]
        public SinhVien SinhVien { get; set; }

        public string MaPhong { get; set; }
        [ForeignKey("MaPhong")]
        public Phong Phong { get; set; }
    }
}