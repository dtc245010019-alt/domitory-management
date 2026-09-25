using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QLKTX.Models
{
    public class HopDong
    {
        [Key]
        [StringLength(20)]
        public string MaHopDong { get; set; }

        public DateTime NgayBatDau { get; set; }
        public DateTime NgayKetThuc { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TienDatCoc { get; set; }

        [StringLength(50)]
        public string TrangThai { get; set; } = "Còn hiệu lực"; // Còn hiệu lực, Hết hiệu lực 

        [StringLength(255)]
        public string DieuKhoan { get; set; }

        // Liên kết Khóa ngoại
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