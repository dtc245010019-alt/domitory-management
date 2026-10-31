# HỆ THỐNG QUẢN LÝ KÝ TÚC XÁ TÍCH HỢP AI (QLKTX)

- **Học phần:** Ứng dụng trí tuệ nhân tạo - AIA331 (Mã đề tài: **80309**)
- **Trường:** Đại học Công Nghệ Thông Tin và Truyền Thông - Đại học Thái Nguyên
- **Nhóm 06:** Nguyễn Khánh Duy, Vũ Văn Dũng, Dương Văn Chung

Hệ thống Quản lý Ký túc xá (**QLKTX**) viết trên **ASP.NET Core 8.0 MVC**, **Entity Framework Core 8.0** và **SQL Server**. Hệ thống có trang quản trị cho Ban Quản lý và Kế toán, **Cổng thông tin Sinh viên (Student Portal)** và **3 chức năng AI** dùng mô hình **`llama3` chạy cục bộ qua Ollama**, kết hợp cơ sở tri thức `knowledge_base.json`.

---

## 📚 TÀI LIỆU KÈM THEO

1. 📄 **[Tài liệu phân tích thiết kế, kiến trúc & kịch bản kiểm thử (`tailieu.md`)](tailieu.md)**
2. 🤖 **[Nhật ký sử dụng AI, tối ưu prompt 3 vòng (`NhatkysudungAI.md`)](NhatkysudungAI.md)**
3. 🗺️ **[Bản đồ mã nguồn (`QLKTX/codebase_map.md`)](QLKTX/codebase_map.md)**
4. 🧪 **[Kết quả kiểm thử (`KET_QUA_KIEM_THU.txt`)](KET_QUA_KIEM_THU.txt)**

---

## 🚀 1. CÔNG NGHỆ SỬ DỤNG
- **Backend:** ASP.NET Core 8.0 MVC (C#)
- **Database / ORM:** Entity Framework Core 8.0, SQL Server / LocalDB
- **Frontend:** Razor Views (`.cshtml`), Bootstrap 5, Bootstrap Icons, Chart.js
- **Excel:** ClosedXML `0.105.0` (nhập/xuất `.xlsx`)
- **AI:** Ollama (`llama3`) + cơ sở tri thức `wwwroot/data/knowledge_base.json`; có phương án dự phòng khi Ollama không chạy
- **Kiểm thử:** xUnit, Moq, EF Core InMemory
- **Đóng gói:** Docker (`Dockerfile`, `docker-compose.yml`)

---

## ✨ 2. CHỨC NĂNG

### 2.1. Phân hệ quản lý ký túc xá
1. **Đăng nhập & phân quyền (`DNController`, `AdminAuthorizeAttribute`):** 3 vai trò **Admin**, **Kế toán (`KeToan`)**, **Sinh viên (`SinhVien`)** qua Session; mật khẩu băm SHA-256 kèm salt (`PasswordHelper`).
2. **Quản lý phòng (`PhongController`):** sĩ số thực tế `X/Y`, lọc theo tòa/tầng và trạng thái (`Còn chỗ`, `Đã đầy`, `Bảo trì`).
3. **Hồ sơ sinh viên (`SinhVienController`):** thêm/sửa/tìm kiếm, nhập/xuất Excel; xóa mềm chuyển sinh viên sang trang Lịch sử (`SinhVien/LichSu`).
4. **Hợp đồng & yêu cầu phòng (`HopDongController`, `PortalController`):** quản lý hợp đồng nội trú, kiểm tra giường trống khi lập hợp đồng; sinh viên gửi yêu cầu chuyển/gia hạn/trả phòng từ Portal.
5. **Hóa đơn & công nợ (`PortalController`):** tính tiền điện nước theo chỉ số cũ/mới, lọc hóa đơn nợ, sinh viên thanh toán trên Portal (phải nhập lại mật khẩu).
6. **Sự cố & vi phạm (`PortalController`, `ViPhamController`):** báo hỏng kèm ảnh, lập biên bản kỷ luật (hình thức "Đình chỉ" tự giải phóng giường).
7. **Tìm kiếm:** không phân biệt hoa/thường; mã phòng gõ `P.A1-03`, `A1-03` hay `a1 03` đều khớp.
8. **Thống kê (`BaoCaoController`):** công suất phòng, tiền cọc, công nợ, sự cố và biểu đồ Chart.js.

### 2.2. Phân hệ AI
1. **🤖 Chatbot (`ChatbotController`):** hỏi đáp nội quy từ `knowledge_base.json` và tra cứu dữ liệu từ CSDL; số điện thoại của sinh viên khác được che, CCCD bị ẩn trong câu trả lời. Chatbot có thể thực hiện một số thao tác (gửi yêu cầu chuyển phòng, tạo báo cáo sự cố) qua `ExecuteAction` sau khi người dùng xác nhận.
2. **✨ Gợi ý xếp phòng (`PhongController.GoiYXepPhongAI`):** chọn 1–3 phòng còn giường theo loại phòng, khu/tầng và số sinh viên cần xếp.
3. **📊 Tóm tắt sự cố (`PortalController.TomTatSuCoAI`):** chia các sự cố đang chờ xử lý thành nhóm Điện, Nước, Khác và nêu sự cố cần xử lý trước.

---

## 📁 3. CẤU TRÚC THƯ MỤC
```text
nhom6_moi/
├── Dockerfile, docker-compose.yml   <-- Đóng gói và chạy bằng Docker
├── README.md                        <-- Hướng dẫn cài đặt (file này)
├── CLAUDE.md, prompt.md             <-- Quy tắc kiến trúc & nghiệp vụ dùng làm ngữ cảnh cho AI
├── tailieu.md                       <-- Tài liệu phân tích thiết kế, kịch bản kiểm thử
├── NhatkysudungAI.md                <-- Nhật ký sử dụng AI
├── KET_QUA_KIEM_THU.txt             <-- Kết quả kiểm thử
├── QLKTX.sln
├── tests/
│   ├── QLKTX.Tests/                 <-- Dự án kiểm thử xUnit
│   └── run_tests.ps1                <-- Script kiểm thử tổng hợp
└── QLKTX/                           <-- Mã nguồn ASP.NET Core 8.0
    ├── Controllers/                 <-- DN, Account, Home, Phong, SinhVien, HopDong, CoSoVatChat,
    │                                    ViPham, BaoCao, Portal, Chatbot
    ├── Models/                      <-- Entity, ApplicationDbContext, DbInitializer, AIPrompts,
    │                                    SafetyGuardrails, PasswordHelper, HoaDonCalculator, AdminAuthorizeAttribute
    ├── Views/                       <-- Razor Views (.cshtml)
    ├── Migrations/                  <-- Lịch sử migration EF Core
    ├── wwwroot/                     <-- CSS, JS, ảnh, data/knowledge_base.json, uploads/suco
    ├── appsettings.json             <-- Chuỗi kết nối CSDL & cấu hình Ollama
    ├── appsettings.Example.json     <-- File cấu hình mẫu
    └── Program.cs                   <-- DI, Session, Middleware
```

---

## 🛠 4. CÀI ĐẶT VÀ CHẠY

### 4.1. Yêu cầu
- **.NET 8.0 SDK** và công cụ `dotnet-ef` (`dotnet tool install --global dotnet-ef`)
- **SQL Server** (LocalDB, Express hoặc Developer)
- **Ollama** (không bắt buộc). Khi Ollama không chạy, các chức năng AI vẫn trả kết quả dựng từ dữ liệu CSDL, nhưng không có câu văn do AI viết.

### 4.2. Các bước
**Bước 1: Khởi động Ollama (không bắt buộc)**
```bash
ollama pull llama3
ollama serve
```

**Bước 2: Cấu hình**
- Kiểm tra `QLKTX/appsettings.json` (mẫu ở `QLKTX/appsettings.Example.json`). Mặc định kết nối `(localdb)\mssqllocaldb`, Ollama tại `http://127.0.0.1:11434/api/chat`, model `llama3`.

**Bước 3: Tạo CSDL và chạy**
```bash
cd QLKTX
dotnet ef database update
dotnet run
```
- Mở trình duyệt: **`http://localhost:5013`**. Lần chạy đầu, `DbInitializer` tự nạp dữ liệu mẫu.

**Chạy bằng Docker:** `docker compose up --build -d` (gồm SQL Server và web; Ollama chạy trên máy host, web gọi qua `host.docker.internal:11434`).

**Chạy kiểm thử:**
```bash
dotnet test tests/QLKTX.Tests
```

---

## 🔑 5. TÀI KHOẢN DEMO (DỮ LIỆU MẪU)

| Vai trò | Tên đăng nhập | Mật khẩu | Quyền chính |
| :--- | :---: | :---: | :--- |
| **Ban Quản lý (Admin)** | `admin` | `123` | Toàn quyền: phòng, sinh viên, hợp đồng, kỷ luật, duyệt sự cố, AI gợi ý phòng, AI tóm tắt sự cố |
| **Kế toán** | `ketoan` | `123` | Hợp đồng, lập hóa đơn tiền phòng & điện nước, theo dõi công nợ, thống kê; không sửa phòng/sinh viên/kỷ luật |
| **Sinh viên** | `DTC001`, `DTC002`, … | `123` | Cổng sinh viên (`/Portal`): xem phòng, thanh toán hóa đơn, gửi sự cố, gửi yêu cầu phòng, hỏi chatbot |

> Đăng nhập được bằng tên đăng nhập hoặc mã sinh viên. Dữ liệu mẫu có sinh viên đang ở mã `DTC001` trở đi và sinh viên đã rời đi mã `DTC801`–`DTC825`.

---
*Nhóm 06 — Học phần Ứng dụng Trí tuệ Nhân tạo (AIA331).*
