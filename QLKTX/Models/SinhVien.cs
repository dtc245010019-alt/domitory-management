using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QLKTX.Models
{
    public class SinhVien
    {
        [Key]
         [StringLength(20)]
        public string MaSV { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        public string HoTen { get; set; }

        [Required]
        public DateTime NgaySinh { get; set; }

        [Required]
        public string Lop { get; set; }

        [Required]
        public string SoDienThoai { get; set; }

       
        [StringLength(20)]
        public string? CCCD { get; set; }

        
        public string? AnhThe { get; set; }

        [Required]
        [StringLength(50)]
        public string TinhTrangLuuTru { get; set; } = "Đang ở";

        // Liên kết với phòng (Khóa ngoại)
        public string? MaPhong { get; set; }
        [ForeignKey("MaPhong")]
        public virtual Phong? Phong { get; set; }
    }
}