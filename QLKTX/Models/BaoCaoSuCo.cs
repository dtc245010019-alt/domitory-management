using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QLKTX.Models
{
    public class BaoCaoSuCo
    {
        [Key]
        [StringLength(20)]
        public string MaSuCo { get; set; }

        [Required]
        [StringLength(100)]
        public string TenSuCo { get; set; }

        [Required]
        public string MoTa { get; set; }

        [StringLength(50)]
        public string MucDoKhanCap { get; set; }

        public string HinhAnh { get; set; } // Lưu đường dẫn file ảnh 

        [StringLength(50)]
        public string TrangThai { get; set; } = "Chờ xử lý"; // Chờ xử lý, Đã xử lý 

        public DateTime NgayBaoCao { get; set; } = DateTime.Now;

        // Liên kết
        [Required]
        public string MaSV { get; set; }
        [ForeignKey("MaSV")]
        public SinhVien SinhVien { get; set; }

        [Required]
        public string MaPhong { get; set; }
        [ForeignKey("MaPhong")]
        public Phong Phong { get; set; }
    }
}