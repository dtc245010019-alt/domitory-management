using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QLKTX.Models
{
    public class CoSoVatChat
    {
        [Key]
        [StringLength(20)]
        public string MaThietBi { get; set; }

        [Required]
        [StringLength(100)]
        public string TenThietBi { get; set; }

        [StringLength(50)]
        public string LoaiThietBi { get; set; }

        [Required]
        public int SoLuong { get; set; }

        [Required]
        [StringLength(50)]
        public string TinhTrang { get; set; } // Ví dụ: Mới, Đang sử dụng, Hỏng

        // Liên kết với Phòng (Vị trí đặt thiết bị)
        [Required]
        public string MaPhong { get; set; }

        [ForeignKey("MaPhong")]
        public virtual Phong Phong { get; set; }
    }
}