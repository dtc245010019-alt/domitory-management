# Bản đồ Mã Nguồn Dự Án (Codebase Map) - QLKTX

Dự án `QLKTX` là hệ thống Quản lý Ký Túc Xá viết trên **ASP.NET Core 8 MVC**, dùng **Entity Framework Core 8** để làm việc với SQL Server.

## 📂 Sơ Đồ Cấu Trúc Tổng Quan
```text
QLKTX/
├── Controllers/         # Controller xử lý request, kiểm tra quyền, truy vấn DB
├── Models/              # Entity, DbContext, dữ liệu mẫu và các lớp dùng chung (AI, bảo mật)
├── Views/               # Giao diện Razor (.cshtml)
├── Migrations/          # Lịch sử migration của Entity Framework
├── wwwroot/             # CSS, JS, ảnh, thư viện, data/knowledge_base.json, uploads/suco
├── appsettings.json     # Chuỗi kết nối DB, cấu hình Ollama, logging
└── Program.cs           # Đăng ký DI, Session, HttpClient "Ollama", health check, middleware
../tests/QLKTX.Tests/    # Dự án kiểm thử xUnit (nằm ngoài thư mục QLKTX)
```

---

## 🛠️ Chi Tiết Các Thư Mục & File Quan Trọng

### 1. `Controllers/`
- `DNController.cs`: Đăng nhập (bằng tên đăng nhập hoặc mã SV), đăng xuất, quên mật khẩu, liên hệ.
- `AccountController.cs`: Chỉ chuyển hướng `Login`/`Logout` về `DNController` (giữ tương thích đường dẫn cũ).
- `HomeController.cs`: Dashboard quản trị.
- `PhongController.cs`: Quản lý phòng, lọc tòa/tầng, sĩ số X/Y, AI gợi ý xếp phòng (`GoiYXepPhongAI`).
- `SinhVienController.cs`: Hồ sơ sinh viên đang ở (`Index`), đã rời đi (`LichSu`), xóa mềm, nhập/xuất Excel.
- `HopDongController.cs`: Hợp đồng nội trú, kiểm tra ngày và giường trống.
- `CoSoVatChatController.cs`, `ViPhamController.cs`: Thiết bị theo phòng; vi phạm kỷ luật (đình chỉ thì giải phóng giường).
- `BaoCaoController.cs`: Số liệu và biểu đồ thống kê.
- `PortalController.cs`: Cổng sinh viên (`Index`, `HoaDon`, `ThanhToan`, `BaoCaoSuCo`) và các trang quản trị hóa đơn/sự cố (`QuanLyHoaDonAdmin`, `TaoHoaDon`, `QuanLySuCoAdmin`, `TomTatSuCoAI`).
- `ChatbotController.cs`: Chatbot AI (`Ask`) và thực hiện thao tác qua khung chat (`ExecuteAction`).

### 2. `Models/`
- **`ApplicationDbContext.cs`**: DbContext; mọi khóa ngoại đặt `DeleteBehavior.Restrict`.
- **`DbInitializer.cs`**: Nạp dữ liệu mẫu khi khởi động (tài khoản, phòng, sinh viên, hợp đồng, hóa đơn…). **Không** tự chạy migration, phải chạy `dotnet ef database update` trước.
- **Entity**: `SinhVien`, `Phong`, `TaiKhoan`, `HopDong`, `HoaDon`, `BaoCaoSuCo`, `CoSoVatChat`, `ViPham`.
- **Lớp dùng chung**:
  - `AdminAuthorizeAttribute.cs`: Bộ lọc phân quyền trang quản trị; `OnlyAdmin = true` chặn tài khoản Kế toán.
  - `PasswordHelper.cs`: Băm và kiểm tra mật khẩu (SHA-256 + salt).
  - `AIPrompts.cs`: Toàn bộ system prompt và khuôn prompt của 3 chức năng AI.
  - `SafetyGuardrails.cs`: Kiểm tra đầu vào (prompt injection, độ dài), che CCCD/SĐT, bỏ liên kết giả trong câu trả lời AI.
  - `HoaDonCalculator.cs`: Tính tiền điện nước, tiền phòng.
  - `HealthChecks.cs`: Health check cho endpoint `/health`.

### 3. `Views/`
- **`Shared/`**: `_Layout.cshtml` (quản trị, sidebar), `_StudentLayout.cshtml` (Cổng sinh viên), `_LoginLayout.cshtml` (đăng nhập, quên mật khẩu).
- **`Home/`**: Dashboard. **`BaoCao/`**: Trang thống kê.
- **`SinhVien/`**: `Index`, `LichSu`, `Create` (có AI gợi ý xếp phòng), `Edit`, `Details`, `Delete`.
- **`Phong/`**: Danh sách phòng (sĩ số X/Y, bộ lọc, AI gợi ý xếp phòng) và các form phòng.
- **`Portal/`**: Trang sinh viên (`Index`, `HoaDon`, `BaoCaoSuCo`) và trang quản trị (`QuanLyHoaDonAdmin`, `TaoHoaDon`, `QuanLySuCoAdmin`).
- **`HopDong/`, `CoSoVatChat/`, `ViPham/`**: Danh sách và form tương ứng.
- **`DN/`**: `Dangnhap`, `QuenMK`, `LienheQuanly`.

### 4. `wwwroot/` & cấu hình
- **`wwwroot/css/site.css`**: CSS dùng chung cho giao diện quản trị và Portal.
- **`wwwroot/data/knowledge_base.json`**: Nội quy, biểu phí làm nguồn cho chatbot.
- **`wwwroot/uploads/suco/`**: Ảnh sinh viên gửi kèm báo cáo sự cố.

---

## 🚀 Hướng Dẫn Nhanh (Dành cho Lập trình viên)
1. **Luồng dữ liệu:** View → Controller → `ApplicationDbContext` → View.
2. **Phân quyền:** Session `Admin_DangNhap` + `VaiTro` (`Admin`/`KeToan`) cho trang quản trị, kiểm tra bằng `[AdminAuthorize]`; Session `MaSV_DangNhap` cho Cổng sinh viên. Riêng các trang quản trị hóa đơn trong `PortalController` dùng `EnsureAdminSession()`.
3. **Thay đổi CSDL:** sửa entity trong `Models/` rồi chạy `dotnet ef migrations add <Ten>` và `dotnet ef database update`.
4. **Chatbot AI:** cần Ollama chạy tại `http://localhost:11434` với model `llama3` để có câu trả lời do AI viết; nếu không, hệ thống trả kết quả dựng từ CSDL và nội quy.
5. **Kiểm thử:** `dotnet test tests/QLKTX.Tests` từ thư mục gốc.
