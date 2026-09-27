# CLAUDE.md

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" -> "Write tests for invalid inputs, then make them pass"
- "Fix the bug" -> "Write a test that reproduces it, then make it pass"
- "Refactor X" -> "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:

```text
1. [Step] -> verify: [check]
2. [Step] -> verify: [check]
3. [Step] -> verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.

---

## 5. Ngữ cảnh dự án (Project Context)

**Hệ thống Quản lý Ký túc xá Sinh viên (QLKTX) — ASP.NET Core 8.0 MVC (Razor Views).**
Nền tảng web quản trị lưu trú ký túc xá toàn diện theo phong cách **SaaS Admin Dashboard** hiện đại, tích hợp **Cổng thông tin Sinh viên (Student Portal)** và **Trợ lý ảo AI (Chatbot)**. Hệ thống giải quyết trọn vẹn vòng đời lưu trú: quản lý tòa nhà/phòng ở, hồ sơ & lịch sử sinh viên, hợp đồng nội trú, cơ sở vật chất, kỷ luật/vi phạm, hóa đơn điện nước/phòng ở, báo cáo sự cố kỹ thuật và thống kê trực quan.

### Tech Stack thực tế

| Hạng mục | Lựa chọn công nghệ | Vai trò trong hệ thống |
| --- | --- | --- |
| **Backend Framework** | ASP.NET Core MVC (.NET 8.0) | Kiến trúc Model - View - Controller, Routing, Middleware & Session |
| **ORM / Database** | Entity Framework Core 8.0 + SQL Server | Lưu trữ dữ liệu quan hệ, LINQ Queries, Code-First Migrations & Seed Data |
| **Frontend / UI** | Razor Views (`.cshtml`) + Bootstrap 5 + Bootstrap Icons | Giao diện đồng bộ chuẩn SaaS (`site.css`), Responsive, Modal & Toast |
| **Biểu đồ & Báo cáo** | Chart.js + ClosedXML (`0.105.0`) | Biểu đồ thống kê trực quan trên Dashboard & Import/Export file Excel (`.xlsx`) |
| **Xác thực & Phiên** | ASP.NET Core Session (`HttpContext.Session`) | Phân quyền truy cập theo Session (`Admin_DangNhap`, `MaSV_DangNhap`, `VaiTro`) |
| **AI Engine (Chatbot)** | Ollama Local LLM (`llama3`) + RAG Knowledge Base | Phân tích Intent, truy vấn DB trực tiếp & Fallback về `data/knowledge_base.json` |

### Sơ đồ Cấu trúc thư mục thực tế

```text
d:\domitory-management\
├── CLAUDE.md                           <- Quy chuẩn kiến trúc & hướng dẫn hành vi cho AI Agent
└── QLKTX/                              <- Thư mục dự án ASP.NET Core MVC chính
    ├── Controllers/                    <- Tầng điều phối HTTP Request, kiểm tra Session & truy vấn DB
    │   ├── HomeController.cs           <- Dashboard tổng quan dành cho Admin
    │   ├── DNController.cs             <- Đăng nhập (Admin & Sinh viên), Đăng xuất, Quên mật khẩu, Liên hệ
    │   ├── AccountController.cs        <- Xử lý xác thực bổ trợ
    │   ├── SinhVienController.cs       <- CRUD Sinh viên đang ở, Lịch sử rời KTX (LichSu), Import/Export Excel
    │   ├── PhongController.cs          <- Quản lý phòng ở, theo dõi sức chứa & sĩ số thực tế (X/Y)
    │   ├── HopDongController.cs        <- Quản lý hợp đồng nội trú & thời hạn hiệu lực
    │   ├── CoSoVatChatController.cs    <- Quản lý tài sản, trang thiết bị theo phòng
    │   ├── ViPhamController.cs         <- Ghi nhận và xử lý kỷ luật / vi phạm nội quy
    │   ├── BaoCaoController.cs         <- Thống kê tổng hợp & biểu đồ phân tích
    │   ├── PortalController.cs         <- Cổng Sinh viên (Index, HoaDon, BaoCaoSuCo) & Quản lý Hóa đơn/Sự cố Admin
    │   └── ChatbotController.cs        <- API Chatbot AI (Ollama llama3 + DB Query + Knowledge Base Fallback)
    ├── Models/                         <- Data Entities, DbContext & Khởi tạo dữ liệu
    │   ├── ApplicationDbContext.cs     <- EF Core DbContext quản lý các DbSet
    │   ├── DbInitializer.cs            <- Tự động migrate & seed dữ liệu mẫu khi khởi chạy
    │   ├── SinhVien.cs                 <- Thực thể Sinh viên (MaSV, HoTen, Lop, MaPhong, TinhTrangLuuTru...)
    │   ├── Phong.cs                    <- Thực thể Phòng (MaPhong, LoaiPhong, SoLuongGiuong, GiaPhong, TinhTrang...)
    │   ├── HopDong.cs                  <- Thực thể Hợp đồng lưu trú
    │   ├── HoaDon.cs                   <- Thực thể Hóa đơn tiền phòng / điện nước
    │   ├── BaoCaoSuCo.cs               <- Thực thể Báo cáo sự cố hỏng hóc thiết bị
    │   ├── CoSoVatChat.cs              <- Thực thể Cơ sở vật chất
    │   ├── ViPham.cs                   <- Thực thể Vi phạm kỷ luật
    │   ├── TaiKhoan.cs                 <- Thực thể Tài khoản đăng nhập (TenDangNhap, MatKhau, VaiTro, MaSV)
    │   └── LoginViewModel.cs           <- ViewModel đăng nhập
    ├── Views/                          <- Tầng giao diện Razor Views (.cshtml)
    │   ├── Shared/
    │   │   ├── _Layout.cshtml          <- Layout chuẩn SaaS Admin (Sidebar cố định + Topbar + Content)
    │   │   ├── _StudentLayout.cshtml   <- Layout Cổng Sinh viên (Topbar đồng bộ màu Primary + Chatbot Widget)
    │   │   └── _LoginLayout.cshtml     <- Layout Đăng nhập/Quên MK (Frosted-glass card trên nền ảnh Campus)
    │   ├── Home/                       <- Trang chủ Dashboard Admin
    │   ├── SinhVien/                   <- Index (Đang ở), LichSu (Đã rời đi), Create, Edit, Details, Delete
    │   ├── Phong/                      <- Index (kèm bộ lọc nhanh & sĩ số X/Y), Create, Edit, Details, Delete
    │   ├── HopDong/                    <- Quản lý danh sách & biểu mẫu hợp đồng
    │   ├── CoSoVatChat/                <- Quản lý danh sách & biểu mẫu thiết bị
    │   ├── ViPham/                     <- Quản lý danh sách & biểu mẫu kỷ luật
    │   ├── BaoCao/                     <- Giao diện báo cáo thống kê
    │   ├── Portal/                     <- Index, HoaDon, BaoCaoSuCo (SV) & QuanLyHoaDonAdmin, QuanLySuCoAdmin (Admin)
    │   └── DN/                         <- Dangnhap, QuenMK, LienheQuanly
    ├── Migrations/                     <- Lịch sử EF Core Migrations
    ├── data/
    │   └── knowledge_base.json         <- Cơ sở tri thức cục bộ cho Chatbot AI
    ├── wwwroot/                        <- Tài nguyên tĩnh (Static Assets)
    │   ├── css/site.css                <- Hệ thống biến CSS (:root) & class chuẩn QLKTX SaaS Design System
    │   ├── js/site.js                  <- Logic tương tác client-side chung
    │   ├── images/                     <- Ảnh nền campus và tài nguyên minh họa
    │   └── uploads/suco/               <- Hình ảnh đính kèm khi sinh viên gửi báo cáo sự cố
    ├── Program.cs                      <- Cấu hình DI, EF Core, Session Cookie Policy & Middleware Pipeline
    └── appsettings.json                <- Chuỗi kết nối SQL Server (DefaultConnection) & cấu hình Logging
```

### Vai trò người dùng (RBAC) & Quản lý Phiên (Session)

Hệ thống phân quyền dựa trên `HttpContext.Session` được thiết lập tại `DNController.Dangnhap`:

| Vai trò | Session Keys nhận diện | Layout sử dụng | Phạm vi quyền hạn |
| --- | --- | --- | --- |
| **Admin (Ban Quản lý)** | `Admin_DangNhap = "true"`<br>`VaiTro = "Admin"` | `_Layout.cshtml` | Toàn quyền CRUD Sinh viên, xem Lịch sử sinh viên đã rời, Quản lý Phòng, Hợp đồng, Cơ sở vật chất, Kỷ luật, duyệt Báo cáo sự cố (`QuanLySuCoAdmin`) và quản lý Hóa đơn (`QuanLyHoaDonAdmin`). |
| **Student (Sinh viên)** | `MaSV_DangNhap = <MaSV>`<br>`VaiTro = "Student"` | `_StudentLayout.cshtml` | Truy cập Cổng Sinh viên (`Portal/Index`), xem thông tin thẻ lưu trú & phòng hiện tại, tra cứu và thanh toán hóa đơn cá nhân (`Portal/HoaDon`), gửi báo cáo hỏng thiết bị kèm ảnh (`Portal/BaoCaoSuCo`), hỏi đáp Chatbot AI. |

### Quy ước Đặt tên & Ngôn ngữ

1. **Giao diện (UI), thông báo lỗi, comment nghiệp vụ:** Sử dụng **Tiếng Việt có dấu chuẩn xác**. Mọi file mã nguồn (`.cs`, `.cshtml`, `.css`, `.json`) bắt buộc lưu với bảng mã **UTF-8** để không xảy ra lỗi hỏng dấu tiếng Việt.
2. **Định danh Code & Database Schema:** Giữ nguyên quy ước **Tiếng Việt không dấu theo chuẩn PascalCase** cho tên Class, Controller, Action và thuộc tính Model để đồng bộ 100% với CSDL hiện có (ví dụ: `SinhVien`, `MaSV`, `TinhTrangLuuTru`, `Phong`, `MaPhong`, `SoLuongGiuong`, `HopDong`, `HoaDon`, `BaoCaoSuCo`).
3. **Không tự ý đổi tên Route/Action hoặc thuộc tính Model:** Mọi thay đổi ở View hoặc Controller phải giữ nguyên tên `asp-controller`, `asp-action` và binding properties đã định nghĩa.

---

## 6. Quy trình làm việc (Session Workflow)

### Mở phiên (Session Start)
1. **Xác định ngữ cảnh:** Đọc kỹ yêu cầu của người dùng, đối chiếu với `CLAUDE.md` để nắm rõ ranh giới kiến trúc và các luật nghiệp vụ bất khả xâm phạm.
2. **Đọc file an toàn với UTF-8:** Luôn đọc file `.cs` và `.cshtml` bằng công cụ đọc file chuẩn UTF-8 (tránh dùng lệnh shell xuất stdout làm biến dạng ký tự tiếng Việt trước khi chỉnh sửa).
3. **Kiểm tra trạng thái thực tế:** Trước khi sửa câu truy vấn LINQ hoặc giao diện Razor, kiểm tra chính xác tên thuộc tính trong `Models/` và giá trị chuỗi thực tế đang lưu trong Database.

### Đóng phiên (Session End)
1. **Kiểm tra biên dịch:** Đảm bảo code C# và cú pháp Razor (`.cshtml`) đóng/mở thẻ HTML chính xác, không làm vỡ bố cục (`container-fluid`) hoặc gây lỗi biên dịch.
2. **Đối chiếu ảnh hưởng chéo:** Nếu thay đổi liên quan đến trạng thái lưu trú hoặc phòng ở, kiểm tra đủ các điểm chạm: Danh sách chính (`SinhVien/Index`), Lịch sử (`SinhVien/LichSu`), Quản lý Phòng (`Phong/Index`), và Dashboard (`Home/Index`).
3. **Báo cáo ngắn gọn, minh bạch:** Liệt kê rõ các file đã tạo mới hoặc chỉnh sửa cùng nguyên lý hoạt động.

---

## 7. Các Luật Nghiệp Vụ Bất Khả Xâm Phạm (Invariant Business Rules)

Mọi thay đổi code trong dự án **QLKTX** tuyệt đối không được vi phạm 5 luật cốt lõi sau:

### Luật 1: Cấm xóa cứng Sinh viên — Quản lý vòng đời qua `TinhTrangLuuTru`
- **Nguyên tắc:** Các bảng `HopDong`, `HoaDon`, `ViPham`, `BaoCaoSuCo` đều tham chiếu khóa ngoại tới `MaSV`. Việc xóa cứng (`DELETE`) bản ghi sinh viên đã từng ở KTX sẽ gây lỗi khóa ngoại hoặc làm mất lịch sử tài chính/kỷ luật.
- **Thực thi:**
  - Trang `SinhVien/Index` (Hồ sơ sinh viên lưu trú) **CHỈ** lọc và hiển thị sinh viên có `TinhTrangLuuTru == "Đang ở"`.
  - Trang `SinhVien/LichSu` (Lịch sử sinh viên đã rời) lọc và hiển thị sinh viên có `TinhTrangLuuTru == "Đã rời đi"`.

### Luật 2: Đồng bộ Sĩ số thực tế (`X/Y`) và Trạng thái Phòng ở
- **Đếm sĩ số phòng:** Sĩ số thực tế của một phòng (`soDangO`) **chỉ được đếm** các sinh viên thuộc phòng đó có `TinhTrangLuuTru == "Đang ở"` (bỏ qua sinh viên `"Đã rời đi"` dù bản ghi vẫn còn lưu `MaPhong`).
- **Tự động giải phóng phòng khi sinh viên rời đi:** Trong `SinhVienController.Edit (POST)`, ngay sau khi lưu sinh viên có trạng thái `"Đã rời đi"`, phải đếm lại số sinh viên `"Đang ở"` trong phòng đó. Nếu `soDangO < phong.SoLuongGiuong` và `phong.TinhTrang == "Đã đầy"`, hệ thống phải tự động cập nhật `phong.TinhTrang = "Còn chỗ"`.

### Luật 3: Phân tách rạch ròi Route và Session giữa Admin và Sinh viên
- **Nguyên tắc:** Khi đăng nhập Admin, hệ thống chỉ gán `Session["Admin_DangNhap"] = "true"`, **không** có `Session["MaSV_DangNhap"]`.
- **Thực thi:**
  - Các action dành cho Sinh viên (`Portal/Index`, `Portal/HoaDon`, `Portal/BaoCaoSuCo`) kiểm tra `EnsureStudentSession()`.
  - Các menu trên Sidebar/Topbar của Admin (`_Layout.cshtml`) tuyệt đối **không** được trỏ vào route của sinh viên, mà phải trỏ vào `Portal/QuanLyHoaDonAdmin` và `Portal/QuanLySuCoAdmin` (sử dụng `EnsureAdminSession()`).
  - Cấu hình Session Cookie trong `Program.cs` chỉ được đăng ký `AddSession` **1 lần duy nhất** với `SameSiteMode.Lax`, `SecurePolicy = SameAsRequest` và đặt `app.UseSession()` đúng vị trí giữa `UseRouting()` và `UseAuthorization()`.

### Luật 4: Chuẩn hóa dữ liệu Đăng nhập và Tìm kiếm thông minh
- **Đăng nhập đa định danh (`DNController.Dangnhap`):** Cho phép người dùng đăng nhập bằng **CẢ `TenDangNhap` LẪN `MaSV`**, luôn gọi `.Trim()` cho `Username` và `Password` trước khi truy vấn.
- **Tìm kiếm Mã phòng không phụ thuộc tiền tố UI:** Giao diện hiển thị mã phòng dạng `"P.A1-03"` (trong đó `"P."` là chuỗi tĩnh trên HTML, còn DB lưu `"A1-03"`). Mọi câu truy vấn tìm kiếm theo mã phòng phải chuẩn hóa từ khóa (cắt bỏ tiền tố `"P."`, xóa dấu chấm `.`, khoảng trắng và dấu gạch ngang `-`, chuyển về chữ hoa) để người dùng gõ `"P.A1-03"`, `"A1-03"`, `"a1-03"` hay `"A1 03"` đều trả về kết quả chính xác.

### Luật 5: Bảo toàn bố cục Full-Width của Hệ thống Thiết kế SaaS
- Các trang danh sách trong phân hệ Admin (`SinhVien/Index`, `SinhVien/LichSu`, `Phong/Index`...) phải được bao ngoài bởi `<div class="container-fluid px-0">` và kiểm soát chặt chẽ số lượng thẻ đóng `</div>` để bảng dữ liệu luôn trải rộng toàn bộ vùng nội dung chính, không bị co hẹp hay lệch bố cục.

---

## 8. Ranh giới Kiến trúc (Architecture Boundaries) & An toàn AI

### Ranh giới các tầng trong ASP.NET Core MVC
1. **Tầng Giao diện (`Views/` & `wwwroot/css/site.css`):**
   - Chỉ đảm nhận hiển thị dữ liệu, định dạng badge/card/table và gửi form. Không viết các câu truy vấn làm thay đổi dữ liệu (INSERT/UPDATE/DELETE) trực tiếp trong file `.cshtml`.
   - Mọi trang thuộc Cổng Sinh viên (`Views/Portal/Index.cshtml`, `HoaDon.cshtml`, `BaoCaoSuCo.cshtml`) và `_StudentLayout.cshtml` phải đồng bộ hoàn toàn ngôn ngữ thiết kế với Admin (`bg-primary`, card `border-0 shadow-sm rounded-4`, badge dạng pill `bg-*-subtle`), không tự ý pha trộn các màu nền rời rạc cũ (`bg-success` đặc) làm lệch tông thiết kế.
2. **Tầng Điều khiển (`Controllers/`):**
   - Chịu trách nhiệm xác thực phiên làm việc (`Session`), chuẩn hóa tham số đầu vào (`searchString`), thực thi truy vấn qua `ApplicationDbContext` (sử dụng `.Include()` khi cần dữ liệu bảng liên kết như `Phong.SinhViens` hoặc `SinhVien.Phong`) và trả về View tương ứng.
3. **Tầng Dữ liệu (`Models/` & `Migrations/`):**
   - Không tự ý thêm cột mới hoặc thay đổi kiểu dữ liệu trong các class Entity nếu không thực hiện đầy đủ quy trình Migration của EF Core.

### Ranh giới & Cơ chế Fallback cho Chatbot AI (`ChatbotController`)
- **Chỉ đọc dữ liệu (Read-Only):** Chatbot chỉ được phép truy vấn (`SELECT` / `Count` / `AsNoTracking`) từ `ApplicationDbContext` để trả lời thông tin thống kê, phòng trống, nội quy; tuyệt đối không thực hiện thao tác ghi/xóa dữ liệu từ lời nhắc của người dùng.
- **Bảo mật thông tin:** Không bao giờ đưa dữ liệu bảng `TaiKhoan` (mật khẩu) hoặc thông tin nhạy cảm vào ngữ cảnh prompt gửi sang LLM.
- **Fallback đa tầng bắt buộc:** Khi gọi Ollama (`http://localhost:11434/api/chat`), luôn bọc trong `try-catch` và kết hợp tra cứu từ `data/knowledge_base.json` để đảm bảo tính năng hỗ trợ sinh viên vẫn phản hồi ổn định ngay cả khi dịch vụ AI cục bộ chưa khởi động.