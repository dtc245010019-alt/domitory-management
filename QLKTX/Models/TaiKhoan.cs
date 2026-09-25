using System.ComponentModel.DataAnnotations;

namespace QLKTX.Models
{
    public class TaiKhoan
    {
        [Key]
        [StringLength(50)]
        public string TenDangNhap { get; set; }

        [Required]
        public string MatKhau { get; set; } // Mật khẩu sẽ được mã hóa 

        [Required]
        [StringLength(20)]
        public string? Quyen { get; set; } // "QuanLy", "NhanVien", "SinhVien" 

        [StringLength(20)]
        public string? MaSV { get; set; } // Chỉ có dữ liệu nếu Quyen là "SinhVien"
    }
}