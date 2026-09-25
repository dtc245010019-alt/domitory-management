using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;
using QLKTX.Models;

namespace QLKTX.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Phong> Phongs { get; set; }
        public DbSet<SinhVien> SinhViens { get; set; }
        public DbSet<CoSoVatChat> CoSoVatChats { get; set; }
        public DbSet<HopDong> HopDongs { get; set; }
        public DbSet<HoaDon> HoaDons { get; set; }
        public DbSet<BaoCaoSuCo> BaoCaoSuCos { get; set; }
        public DbSet<TaiKhoan> TaiKhoans { get; set; }
        public DbSet<ViPham> ViPhams { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Ngăn chặn lỗi cascade delete (xóa phòng thì xóa sạch dữ liệu liên quan)
            foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            {
                relationship.DeleteBehavior = DeleteBehavior.Restrict;
            }
        }
    }
}