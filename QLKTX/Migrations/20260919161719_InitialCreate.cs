using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QLKTX.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Phongs",
                columns: table => new
                {
                    MaPhong = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LoaiPhong = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SoLuongGiuong = table.Column<int>(type: "int", nullable: false),
                    GiaPhong = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TrangThaiThietBi = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    TinhTrang = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Phongs", x => x.MaPhong);
                });

            migrationBuilder.CreateTable(
                name: "TaiKhoans",
                columns: table => new
                {
                    TenDangNhap = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MatKhau = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quyen = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MaSV = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaiKhoans", x => x.TenDangNhap);
                });

            migrationBuilder.CreateTable(
                name: "CoSoVatChats",
                columns: table => new
                {
                    MaThietBi = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TenThietBi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LoaiThietBi = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SoLuong = table.Column<int>(type: "int", nullable: false),
                    TinhTrang = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MaPhong = table.Column<string>(type: "nvarchar(20)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoSoVatChats", x => x.MaThietBi);
                    table.ForeignKey(
                        name: "FK_CoSoVatChats_Phongs_MaPhong",
                        column: x => x.MaPhong,
                        principalTable: "Phongs",
                        principalColumn: "MaPhong",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SinhViens",
                columns: table => new
                {
                    MaSV = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    HoTen = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NgaySinh = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Lop = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SoDienThoai = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CCCD = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AnhThe = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TinhTrangLuuTru = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MaPhong = table.Column<string>(type: "nvarchar(20)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SinhViens", x => x.MaSV);
                    table.ForeignKey(
                        name: "FK_SinhViens_Phongs_MaPhong",
                        column: x => x.MaPhong,
                        principalTable: "Phongs",
                        principalColumn: "MaPhong",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BaoCaoSuCos",
                columns: table => new
                {
                    MaSuCo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TenSuCo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MoTa = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MucDoKhanCap = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    HinhAnh = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NgayBaoCao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MaSV = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    MaPhong = table.Column<string>(type: "nvarchar(20)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BaoCaoSuCos", x => x.MaSuCo);
                    table.ForeignKey(
                        name: "FK_BaoCaoSuCos_Phongs_MaPhong",
                        column: x => x.MaPhong,
                        principalTable: "Phongs",
                        principalColumn: "MaPhong",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BaoCaoSuCos_SinhViens_MaSV",
                        column: x => x.MaSV,
                        principalTable: "SinhViens",
                        principalColumn: "MaSV",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HoaDons",
                columns: table => new
                {
                    MaHoaDon = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LoaiHoaDon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ChiSoCu = table.Column<int>(type: "int", nullable: true),
                    ChiSoMoi = table.Column<int>(type: "int", nullable: true),
                    DonGia = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TongTien = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HinhThucThanhToan = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NgayLap = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MaSV = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    MaPhong = table.Column<string>(type: "nvarchar(20)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HoaDons", x => x.MaHoaDon);
                    table.ForeignKey(
                        name: "FK_HoaDons_Phongs_MaPhong",
                        column: x => x.MaPhong,
                        principalTable: "Phongs",
                        principalColumn: "MaPhong",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HoaDons_SinhViens_MaSV",
                        column: x => x.MaSV,
                        principalTable: "SinhViens",
                        principalColumn: "MaSV",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HopDongs",
                columns: table => new
                {
                    MaHopDong = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NgayBatDau = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NgayKetThuc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TienDatCoc = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DieuKhoan = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MaSV = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    MaPhong = table.Column<string>(type: "nvarchar(20)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HopDongs", x => x.MaHopDong);
                    table.ForeignKey(
                        name: "FK_HopDongs_Phongs_MaPhong",
                        column: x => x.MaPhong,
                        principalTable: "Phongs",
                        principalColumn: "MaPhong",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HopDongs_SinhViens_MaSV",
                        column: x => x.MaSV,
                        principalTable: "SinhViens",
                        principalColumn: "MaSV",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ViPhams",
                columns: table => new
                {
                    MaViPham = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NoiDungViPham = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    HinhThucXuLy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NgayViPham = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GhiChu = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MaSV = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ViPhams", x => x.MaViPham);
                    table.ForeignKey(
                        name: "FK_ViPhams_SinhViens_MaSV",
                        column: x => x.MaSV,
                        principalTable: "SinhViens",
                        principalColumn: "MaSV",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BaoCaoSuCos_MaPhong",
                table: "BaoCaoSuCos",
                column: "MaPhong");

            migrationBuilder.CreateIndex(
                name: "IX_BaoCaoSuCos_MaSV",
                table: "BaoCaoSuCos",
                column: "MaSV");

            migrationBuilder.CreateIndex(
                name: "IX_CoSoVatChats_MaPhong",
                table: "CoSoVatChats",
                column: "MaPhong");

            migrationBuilder.CreateIndex(
                name: "IX_HoaDons_MaPhong",
                table: "HoaDons",
                column: "MaPhong");

            migrationBuilder.CreateIndex(
                name: "IX_HoaDons_MaSV",
                table: "HoaDons",
                column: "MaSV");

            migrationBuilder.CreateIndex(
                name: "IX_HopDongs_MaPhong",
                table: "HopDongs",
                column: "MaPhong");

            migrationBuilder.CreateIndex(
                name: "IX_HopDongs_MaSV",
                table: "HopDongs",
                column: "MaSV");

            migrationBuilder.CreateIndex(
                name: "IX_SinhViens_MaPhong",
                table: "SinhViens",
                column: "MaPhong");

            migrationBuilder.CreateIndex(
                name: "IX_ViPhams_MaSV",
                table: "ViPhams",
                column: "MaSV");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BaoCaoSuCos");

            migrationBuilder.DropTable(
                name: "CoSoVatChats");

            migrationBuilder.DropTable(
                name: "HoaDons");

            migrationBuilder.DropTable(
                name: "HopDongs");

            migrationBuilder.DropTable(
                name: "TaiKhoans");

            migrationBuilder.DropTable(
                name: "ViPhams");

            migrationBuilder.DropTable(
                name: "SinhViens");

            migrationBuilder.DropTable(
                name: "Phongs");
        }
    }
}
