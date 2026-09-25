using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QLKTX.Models
{



    public class Phong
    {
        [Key]
        [StringLength(20)]
        public string MaPhong { get; set; }

        [Required]
        [StringLength(50)]
        public string LoaiPhong { get; set; }

        [Required]
        public int SoLuongGiuong { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")] 
        public decimal GiaPhong { get; set; }

        [StringLength(255)]
        public string? TrangThaiThietBi { get; set; }

        [Required]
        [StringLength(50)]
        public string TinhTrang { get; set; } = "Còn chỗ";

        // Navigation property: Mối quan hệ 1-N (1 Phòng có nhiều Sinh viên)
        public ICollection<SinhVien> SinhViens { get; set; }

    }
}