# HỆ THỐNG QUẢN LÝ KÝ TÚC XÁ TÍCH HỢP AI (QLKTX)

- **Học phần:** Ứng dụng trí tuệ nhân tạo - AIA331 (Mã đề tài: **80309**)
- **Trường:** Đại học Công Nghệ Thông Tin và Truyền Thông - Đại học Thái Nguyên
- **Sinh viên thực hiện:** Nguyễn Khánh Duy

Hệ thống Quản lý Ký túc xá (**QLKTX**) được phát triển trên nền tảng **ASP.NET Core 8.0 MVC**, kết hợp **Entity Framework Core 8.0** và cơ sở dữ liệu **SQL Server**. Hệ thống cung cấp giao diện quản trị chuẩn **SaaS Dashboard** dành cho Ban Quản lý & Kế toán, **Cổng thông tin Sinh viên (Student Portal)** và tích hợp **3 tính năng Trí tuệ Nhân tạo (AI)** sử dụng mô hình ngôn ngữ lớn **Ollama (`llama3`)** kết hợp cơ sở tri thức **RAG (`knowledge_base.json`)**.

---

## 📚 TÀI LIỆU KỸ THUẬT & MINH CHỨNG ĐÁNH GIÁ (KT1, KT2, KT3, CUỐI KỲ)

Toàn bộ hồ sơ phân tích thiết kế, sơ đồ Use Case, ERD, Kiến trúc hệ thống, kịch bản kiểm thử (Test Cases) và nhật ký sử dụng AI được trình bày chi tiết tại:
1. 📄 **[Tài liệu Phân tích Thiết kế, Kiến trúc & Kịch bản Kiểm thử (`tailieu.md`)](tailieu.md)** *(Đáp ứng KT1, KT3 & Báo cáo kỹ thuật cuối kỳ)*
2. 🤖 **[Nhật ký & Minh chứng Sử dụng AI, Tối ưu Prompt 3 vòng (`QLKTX/NhatkysudungAI.md`)](QLKTX/NhatkysudungAI.md)** *(Đáp ứng KT1, KT2, KT3)*
3. 🗺️ **[Bản đồ Mã nguồn (`QLKTX/codebase_map.md`)](QLKTX/codebase_map.md)**

---

## 🚀 1. CÔNG NGHỆ SỬ DỤNG
- **Backend Framework:** ASP.NET Core 8.0 MVC (C#)
- **Database / ORM:** Entity Framework Core 8.0, SQL Server / LocalDB
- **Frontend / UI:** HTML5, CSS3, Razor Views (`.cshtml`), Bootstrap 5, Bootstrap Icons, Chart.js
- **Xử lý báo cáo Excel:** ClosedXML (`0.105.0` - Import/Export `.xlsx`)
- **AI Engine:** Ollama Local LLM (`llama3`) + RAG Knowledge Base (`wwwroot/data/knowledge_base.json`) + Multi-layer Smart Fallback
- **Đóng gói & Triển khai:** Docker (`Dockerfile` & `docker-compose.yml`)

---

## ✨ 2. CÁC CHỨC NĂNG CỐT LÕI

### 2.1. Phân hệ Quản lý Ký túc xá (8 Nhóm chức năng)
1. **Đăng nhập & Phân quyền (`DNController`, `AdminAuthorizeAttribute`):** Phân quyền 3 vai trò **Quản lý KTX (`Admin`)**, **Kế toán (`KeToan`)**, và **Sinh viên (`SinhVien`)** qua Session bảo mật.
2. **Quản lý Tòa, Tầng, Phòng & Số giường (`PhongController`):** Theo dõi sức chứa, sĩ số thực tế `X/Y`, lọc nhanh theo Tòa/Tầng và trạng thái (`Còn chỗ`, `Đã đầy`, `Bảo trì`).
3. **Quản lý Hồ sơ Sinh viên & Lịch sử ở (`SinhVienController`):** CRUD sinh viên, nhập/xuất Excel, xóa mềm chuyển vào Lịch sử rời KTX (`SinhVien/LichSu`).
4. **Đăng ký ở, Xếp phòng, Chuyển phòng & Trả phòng (`HopDongController`, `PortalController`):** Quản lý hợp đồng nội trú, tự động đồng bộ chỗ trống khi trả phòng; sinh viên gửi yêu cầu đăng ký/chuyển phòng từ Portal.
5. **Theo dõi Phí phòng, Điện nước & Công nợ (`PortalController`):** Tính hóa đơn điện nước theo chỉ số cũ/mới, lọc hóa đơn nợ và thanh toán trực tuyến trên Portal.
6. **Quản lý Phản ánh/Sự cố & Vi phạm Nội quy (`PortalController`, `ViPhamController`):** Báo hỏng thiết bị kèm hình ảnh, lập biên bản kỷ luật (tự động giải phóng phòng khi đình chỉ).
7. **Tra cứu Chỗ trống & Lịch sử Lưu trú:** Tìm kiếm thông minh không phân biệt hoa/thường hay ký tự phân cách.
8. **Dashboard Thống kê Toàn diện (`BaoCaoController`):** Thống kê công suất phòng, tổng doanh thu, tổng công nợ chưa thu, số lượng phản ánh/sự cố và biểu đồ trực quan.

### 2.2. Phân hệ Trí tuệ Nhân tạo (3 Chức năng AI)
1. **🤖 Chatbot AI Hỏi đáp Nội quy KTX (`ChatbotController`):** Trả lời tự động quy định KTX từ `knowledge_base.json` và số liệu thực tế từ CSDL (đã lọc ẩn thông tin nhạy cảm `CCCD`, `SĐT`).
2. **✨ AI Gợi ý Xếp phòng (`PhongController.GoiYXepPhongAI`):** Phân tích danh sách phòng còn giường trống theo tiêu chí (Loại phòng, Khu/Tầng, Số lượng SV) và đề xuất Top phòng tối ưu.
3. **📊 AI Tóm tắt Phản ánh/Sự cố (`PortalController.TomTatSuCoAI`):** Tự động phân nhóm các sự cố đang chờ xử lý thành nhóm Điện, Nước, Nội thất và đề xuất thứ tự ưu tiên khắc phục.

---

## 📁 3. CẤU TRÚC THƯ MỤC DỰ ÁN
```text
domitory-management/
├── Dockerfile                      <-- Cấu hình đóng gói Docker container
├── docker-compose.yml              <-- Khởi chạy nhanh bằng Docker Compose
├── README.md                       <-- Hướng dẫn cài đặt và thuyết minh dự án
├── tailieu.md                      <-- Tài liệu Phân tích Thiết kế (ERD, UseCase, Kiến trúc, Test Cases)
├── CLAUDE.md                       <-- Quy chuẩn kiến trúc & 5 Luật nghiệp vụ cốt lõi
├── QLKTX.sln                       <-- File Solution Visual Studio
└── QLKTX/                          <-- Thư mục Mã nguồn chính ASP.NET Core 8.0
    ├── Controllers/                <-- Các Controller xử lý nghiệp vụ & AI
    │   ├── HomeController.cs           (Dashboard Admin)
    │   ├── DNController.cs             (Đăng nhập, Đăng xuất, Quên mật khẩu, Liên hệ)
    │   ├── SinhVienController.cs       (Quản lý Sinh viên, Lịch sử & Import/Export Excel)
    │   ├── PhongController.cs          (Quản lý Phòng, Lọc Tòa/Tầng & AI Gợi ý xếp phòng)
    │   ├── HopDongController.cs        (Quản lý Hợp đồng nội trú)
    │   ├── CoSoVatChatController.cs    (Quản lý Cơ sở vật chất)
    │   ├── ViPhamController.cs         (Quản lý Kỷ luật / Vi phạm nội quy)
    │   ├── BaoCaoController.cs         (Báo cáo thống kê Công suất, Công nợ, Sự cố)
    │   ├── PortalController.cs         (Cổng Sinh viên, Hóa đơn, Sự cố & AI Tóm tắt sự cố)
    │   └── ChatbotController.cs        (Trợ lý AI RAG hỏi đáp nội quy & tra cứu KTX)
    ├── Models/                     <-- Định nghĩa Entities, DbContext & AI Prompts
    │   ├── ApplicationDbContext.cs     (EF Core DbContext kết nối SQL Server)
    │   ├── DbInitializer.cs            (Tự động khởi tạo dữ liệu mẫu Seed Data)
    │   ├── AIPrompts.cs                (Quản lý tập trung System/User Prompts cho 3 tính năng AI)
    │   ├── AdminAuthorizeAttribute.cs  (Filter bảo vệ phân quyền các trang Quản trị/Kế toán)
    │   └── SinhVien.cs, Phong.cs, HopDong.cs, HoaDon.cs, BaoCaoSuCo.cs, ViPham.cs...
    ├── Views/                      <-- Giao diện Razor Pages (.cshtml)
    ├── wwwroot/                    <-- Static files (CSS, JS, Uploads ảnh, data/knowledge_base.json)
    ├── NhatkysudungAI.md           <-- Nhật ký Prompt, 3 vòng tối ưu & Review code bằng AI
    ├── appsettings.json            <-- Cấu hình Database & Ollama AI Service
    ├── appsettings.Example.json    <-- File mẫu cấu hình môi trường
    └── Program.cs                  <-- Khởi chạy ứng dụng, DI & Middleware Pipeline
```

---

## 🛠 4. HƯỚNG DẪN CÀI ĐẶT VÀ CHẠY DỰ ÁN

### 1. Yêu cầu môi trường
- **.NET 8.0 SDK**
- **SQL Server** (SQL Server LocalDB hoặc SQL Server Express/Developer)
- **Ollama** (Khuyến khích cài đặt để trải nghiệm LLM; nếu chưa bật Ollama, hệ thống vẫn hoạt động 100% nhờ cơ chế **Smart Fallback**).

### 2. Các bước khởi chạy
**Bước 1: Khởi động dịch vụ AI Ollama (Tùy chọn)**
```bash
ollama run llama3
```

**Bước 2: Cấu hình chuỗi kết nối (Nếu cần)**
- Kiểm tra file `QLKTX/appsettings.json` (mẫu tại `QLKTX/appsettings.Example.json`). Mặc định hệ thống kết nối sẵn tới `(localdb)\mssqllocaldb`.

**Bước 3: Khởi tạo CSDL và Chạy ứng dụng**
```bash
cd QLKTX
dotnet ef database update
dotnet run
```
- Mở trình duyệt truy cập: **`http://localhost:5013`**

*(Hoặc chạy qua Docker: `docker compose up --build -d`)*

---

## 🔑 5. THÔNG TIN TÀI KHOẢN DEMO (AUTO SEED DATA)

Khi khởi chạy lần đầu, hệ thống tự động tạo dữ liệu mẫu đầy đủ. Bạn có thể đăng nhập với các tài khoản sau:

| Vai trò | Tên đăng nhập | Mật khẩu | Quyền hạn chính |
| :--- | :---: | :---: | :--- |
| **Ban Quản lý (Admin)** | `admin` | `123` | Toàn quyền quản lý Phòng, Sinh viên, Hợp đồng, Kỷ luật, Duyệt sự cố, AI Gợi ý phòng & AI Tóm tắt sự cố. |
| **Kế toán (Accountant)** | `ketoan` | `123` | Quản lý Hợp đồng, Lập hóa đơn tiền phòng & điện nước, Theo dõi công nợ & Báo cáo thống kê. |
| **Sinh viên (Student)** | `DTC001` *(tới `DTC040`)* | `123` | Truy cập Cổng Sinh viên (`/Portal`), xem phòng ở, thanh toán hóa đơn, gửi báo cáo sự cố, xin chuyển phòng & hỏi đáp Chatbot AI. |

---
*Dự án thực hiện bởi sinh viên Nguyễn Khánh Duy — Học phần Ứng dụng Trí tuệ Nhân tạo (AIA331).*
