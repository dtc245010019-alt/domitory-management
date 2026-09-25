# HỆ THỐNG QUẢN LÝ KÝ TÚC XÁ (QLKTX)

Hệ thống Quản lý Ký túc xá (QLKTX) được phát triển trên nền tảng **ASP.NET Core 8.0 MVC**, kết hợp **Entity Framework Core 8.0** và CSDL **SQL Server**. Hệ thống tích hợp **Portal dành riêng cho sinh viên** cùng **Chatbot AI tự động** tra cứu từ cơ sở tri thức local (`knowledge_base.json`).

---

## 🚀 1. CÔNG NGHỆ SỬ DỤNG
- **Framework backend**: ASP.NET Core 8.0 (MVC)
- **Database / ORM**: Entity Framework Core 8.0, SQL Server 2019+
- **Frontend**: HTML5, CSS3, Bootstrap 5, JavaScript (Fetch API / AJAX)
- **Thư viện xuất báo cáo**: ClosedXML (Xuất dữ liệu ra file Excel `.xlsx`)
- **Chatbot AI**: Local Knowledge-Base Chatbot (`System.Text.Json` so khớp từ khóa)

---

## 📁 2. CẤU TRÚC THƯ MỤC DỰ ÁN
```text
QLKTX-master/
├── .gitignore
├── .gitattributes
├── README.md                   <-- File hướng dẫn và thuyết minh dự án
├── NhatKy_SuDung_AI.md         <-- Nhật ký minh chứng sử dụng AI (KT1 & KT2)
├── QLKTX.sln                   <-- File Solution Visual Studio
└── QLKTX/                      <-- Thư mục Mã nguồn chính
    ├── Controllers/            <-- Các Controller xử lý nghiệp vụ
    │   ├── AccountController.cs    (Đăng nhập / Đăng xuất)
    │   ├── BaoCaoController.cs     (Báo cáo thống kê)
    │   ├── ChatbotController.cs    (Xử lý trả lời tự động cho Chatbot)
    │   ├── CoSoVatChatController.cs(Quản lý trang thiết bị KTX)
    │   ├── DNController.cs         (Quản lý chỉ số Điện - Nước)
    │   ├── HomeController.cs       (Trang chủ Admin)
    │   ├── HopDongController.cs    (Quản lý Hợp đồng ở KTX)
    │   ├── PhongController.cs      (Quản lý danh mục Phòng ở)
    │   ├── PortalController.cs     (Giao diện dành riêng cho Sinh viên)
    │   ├── SinhVienController.cs   (Quản lý Sinh viên & Xuất Excel)
    │   └── ViPhamController.cs     (Ghi nhận sinh viên vi phạm)
    ├── Models/                 <-- Định nghĩa Entities và ViewModels
    │   ├── ApplicationDbContext.cs (DbContext kết nối SQL Server)
    │   ├── DbInitializer.cs        (Khởi tạo dữ liệu mẫu tự động)
    │   ├── SinhVien.cs, Phong.cs, HopDong.cs, HoaDon.cs...
    ├── Migrations/             <-- Lịch sử tạo bảng CSDL EF Core
    ├── Views/                  <-- Giao diện Razor Pages
    │   ├── Shared/
    │   │   ├── _Layout.cshtml        (Layout chung cho Admin)
    │   │   └── _StudentLayout.cshtml (Layout Portal Sinh viên + Chatbot)
    │   ├── Portal/             (Trang xem hóa đơn & Báo cáo sự cố)
    │   └── [SinhVien, Phong, DN, HopDong...]
    ├── data/
    │   └── knowledge_base.json <-- Cơ sở dữ liệu tri thức mẫu cho Chatbot
    ├── wwwroot/                <-- Static files (CSS, JS, Uploads ảnh)
    ├── appsettings.json        <-- Cấu hình chuỗi kết nối Database
    └── Program.cs              <-- Khởi chạy ứng dụng & Dependency Injection