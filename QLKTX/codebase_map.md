# Bản đồ Mã Nguồn Dự Án (Codebase Map) - QLKTX

Dự án `QLKTX` là một hệ thống Quản lý Ký Túc Xá được xây dựng trên nền tảng **ASP.NET Core MVC**, sử dụng **Entity Framework Core** để tương tác với cơ sở dữ liệu.

Dưới đây là sơ đồ cấu trúc thư mục và vai trò của từng thành phần trong dự án:

## 📂 Sơ Đồ Cấu Trúc Tổng Quan
```text
QLKTX/
├── Controllers/         # Chứa các Controller xử lý luồng logic nghiệp vụ
├── Models/              # Chứa các Entity class, Database Context và Logic Seed Data
├── Views/               # Chứa các file giao diện Razor (.cshtml)
├── Migrations/          # Các file lịch sử cập nhật CSDL của Entity Framework
├── wwwroot/             # Tài nguyên tĩnh: CSS, JavaScript, Hình ảnh, Thư viện
├── data/                # Chứa file dữ liệu JSON (ví dụ: knowledge_base.json cho AI)
├── appsettings.json     # Cấu hình hệ thống (Chuỗi kết nối DB, Log level...)
└── Program.cs           # File khởi chạy ứng dụng, cấu hình Middleware và Service
```

---

## 🛠️ Chi Tiết Các Thư Mục & File Quan Trọng

### 1. Thư mục `Controllers/` (Bộ điều khiển logic)
Điều hướng yêu cầu từ người dùng và gọi Models để xử lý dữ liệu trước khi trả về Views.
- `ChatbotController.cs`: Xử lý logic của Trợ lý ảo AI (Ollama - Llama3), áp dụng mô hình RAG để lọc câu trả lời nội bộ.
- `SinhVienController.cs`: Xử lý CRUD (Thêm, sửa, xóa, xem chi tiết) Hồ sơ Sinh viên lưu trú.
- `PhongController.cs`: Quản lý danh sách phòng, sức chứa và tình trạng phòng.
- `PortalController.cs`: Chứa cả luồng chức năng của Sinh viên (Student Portal) và quản trị tiện ích của Admin (Quản lý hóa đơn, sự cố).
- `DNController.cs / AccountController.cs`: Quản lý đăng nhập, xác thực và phân quyền (Session).
- `BaoCaoController.cs`: Trích xuất và thống kê dữ liệu.
- `HopDongController.cs`, `HoaDonController.cs`, `CoSoVatChatController.cs`, `ViPhamController.cs`: Quản lý các module tương ứng.

### 2. Thư mục `Models/` (Định nghĩa dữ liệu)
Định nghĩa các bảng trong CSDL và ràng buộc dữ liệu.
- **`ApplicationDbContext.cs`**: File cấu hình EF Core trung tâm, kết nối các Entity với bảng SQL.
- **`DbInitializer.cs`**: File tạo dữ liệu mẫu tự động (Seed Data) khi hệ thống khởi chạy lần đầu (Tài khoản, Sinh viên, Phòng, Hóa đơn...).
- **Các Entity chính**: 
  - `SinhVien.cs`, `Phong.cs`, `TaiKhoan.cs`, `HopDong.cs`
  - `HoaDon.cs`, `BaoCaoSuCo.cs`, `CoSoVatChat.cs`, `ViPham.cs`

### 3. Thư mục `Views/` (Giao diện người dùng)
Được viết bằng Razor Syntax (`HTML` + `C#`) và thiết kế theo chuẩn UI/UX "SaaS Theme".
- **`Shared/`**: 
  - `_Layout.cshtml`: Layout chung dành cho màn hình Admin (Thanh công cụ, Sidebar).
  - `_LoginLayout.cshtml`: Layout tối giản dành cho trang Đăng nhập / Quên mật khẩu.
  - `_StudentLayout.cshtml`: Layout dành riêng cho trang cá nhân của Sinh viên (Portal).
- **`Home/`**: Chứa `Index.cshtml` (Dashboard thống kê tổng quan hệ thống).
- **`Portal/`**: Các giao diện hóa đơn, báo cáo sự cố (có cả view dành cho Admin như `QuanLyHoaDonAdmin.cshtml`, `TaoHoaDon.cshtml` và view dành cho Sinh viên).
- **`SinhVien/`**: `Index.cshtml` (Danh sách lưu trú), `LichSu.cshtml` (Lịch sử rời đi), `Create.cshtml`, `Edit.cshtml`.
- **`Phong/`**: Các View quản lý cơ sở vật chất phòng.
- **`DN/`**: Các màn hình đăng nhập (`Dangnhap.cshtml`), quên mật khẩu, liên hệ.

### 4. Thư mục `wwwroot/` & Tài nguyên cấu hình
- **`wwwroot/css/site.css`**: Nơi định nghĩa các CSS class tùy chỉnh (như `shadow-xs`, `stat-widget-card`...) để tạo nên giao diện SaaS đồng nhất.
- **`data/knowledge_base.json`**: Kho dữ liệu (Knowledge Base) tĩnh dùng để làm nguồn tham chiếu cho Chatbot trả lời câu hỏi nội quy.
- **`Program.cs`**: Nơi cấu hình `DbContext`, `Session`, `HttpClient` (dành cho Ollama), và luồng chạy HTTP.

---

## 🚀 Hướng Dẫn Nhanh (Dành cho Lập trình viên)
1. **Kiến trúc luồng dữ liệu:** Giao diện (View) -> Controller nhận Request -> Tương tác DB qua `ApplicationDbContext` (Model) -> Trả về kết quả (View).
2. **Quản lý Session:** Hệ thống phân quyền cơ bản dựa trên Session (`Admin_DangNhap`, `User_DangNhap`). Các Controller bắt buộc phải có phương thức `EnsureAdminSession` hoặc tương đương ở đầu mỗi Action bảo mật.
3. **Cập nhật Database:** Nếu có thay đổi trong thư mục `Models/`, cần chạy lệnh `Add-Migration <Ten>` và `Update-Database` qua Package Manager Console.
4. **Chatbot AI:** Yêu cầu phần mềm Ollama (với model `llama3`) đang chạy ngầm ở port `11434` (địa chỉ `http://localhost:11434`) để hoạt động đầy đủ.
