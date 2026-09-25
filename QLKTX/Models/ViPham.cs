using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QLKTX.Models
{
    public class ViPham
    {
        [Key]
        public int MaViPham { get; set; } // Khóa chính tự động tăng

        [Required(ErrorMessage = "Vui lòng nhập nội dung vi phạm")]
        [StringLength(255)]
        public string NoiDungViPham { get; set; } // VD: Về khuya quá giờ, Đun nấu trong phòng...

        [StringLength(100)]
        public string HinhThucXuLy { get; set; } // VD: Nhắc nhở, Cảnh cáo, Phạt tiền, Đình chỉ

        public DateTime NgayViPham { get; set; } = DateTime.Now;

        [StringLength(255)]
        public string GhiChu { get; set; }

        // Liên kết khóa ngoại sang bảng SinhVien
        [Required]
        [StringLength(20)]
        public string MaSV { get; set; }

        [ForeignKey("MaSV")]
        public virtual SinhVien? SinhVien { get; set; }
    }
}