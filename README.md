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

## Phần 3. HƯỚNG DẪN CÀI ĐẶT VÀ CHẠY DỰ ÁN (Dành cho Giảng viên chấm bài)

### 1. Yêu cầu môi trường
- **.NET 8.0 SDK** được cài đặt trên máy.
- **SQL Server** (LocalDB hoặc SQL Server Management Studio).
- **Ollama** (Bắt buộc phải cài đặt và khởi chạy để test các chức năng AI).

### 2. Các bước khởi chạy dự án
**Bước 1: Khởi động hệ thống AI (Ollama)**
- Mở Terminal (hoặc Command Prompt) và chạy lệnh sau để kéo và chạy mô hình ngôn ngữ lớn:
  ```bash
  ollama run llama3
  ```
- *Lưu ý:* Phải giữ cho Ollama chạy ngầm thì Chatbot và các tính năng Tóm tắt/Gợi ý mới hoạt động.

**Bước 2: Cấu hình cơ sở dữ liệu**
- Mở file `QLKTX/appsettings.json`.
- Kiểm tra chuỗi kết nối `DefaultConnection`. (Mặc định dự án đang sử dụng bảo mật tích hợp Windows Authentication, có thể đổi `Server=...` cho khớp với tên server của bạn).

**Bước 3: Biên dịch và chạy mã nguồn**
- Mở Terminal trỏ vào thư mục `QLKTX`.
- Gõ lệnh để tự động tạo các bảng trong Database:
  ```bash
  dotnet ef database update
  ```
- Khởi động web server:
  ```bash
  dotnet run
  ```
- Truy cập vào địa chỉ được cấp (thường là `http://localhost:5013`).

### 3. Thông tin tài khoản dữ liệu mẫu (Seed Data)
Để thuận tiện cho quá trình chấm điểm, hệ thống đã được cài đặt tự động sinh dữ liệu mẫu (Auto Seed Data). Dưới đây là thông tin đăng nhập:

- **Tài khoản Ban Quản lý (Admin):**
  - Tên đăng nhập: `admin`
  - Mật khẩu: `123`

- **Tài khoản Sinh viên (Test Portal & Chatbot):**
  - Nhập Mã sinh viên bất kỳ trong cơ sở dữ liệu (VD: `DTC321`, `DTC322`, `DTC323`...)
  - Mật khẩu: `123`

---
*Bản quyền dự án thuộc về sinh viên Nguyễn Khánh Duy.*