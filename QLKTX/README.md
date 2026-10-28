# HỆ THỐNG QUẢN LÝ KÝ TÚC XÁ TÍCH HỢP AI (QLKTX)

- **Học phần:** Ứng dụng trí tuệ nhân tạo - AIA331 (Mã đề tài: **80309**)
- **Trường:** Đại học Công Nghệ Thông Tin và Truyền Thông - Đại học Thái Nguyên
- **Sinh viên thực hiện:** Nguyễn Khánh Duy

Hệ thống Quản lý Ký túc xá (**QLKTX**) được phát triển trên nền tảng **ASP.NET Core 8.0 MVC**, kết hợp **Entity Framework Core 8.0** và cơ sở dữ liệu **SQL Server**. Hệ thống cung cấp giao diện quản trị chuẩn **SaaS Dashboard** dành cho Ban Quản lý & Kế toán, **Cổng thông tin Sinh viên (Student Portal)** và tích hợp **3 tính năng Trí tuệ Nhân tạo (AI)** sử dụng mô hình ngôn ngữ lớn **Ollama (`llama3`)** kết hợp cơ sở tri thức **RAG (`knowledge_base.json`)**.

---

## 📚 TÀI LIỆU KỸ THUẬT & MINH CHỨNG ĐÁNH GIÁ
1. 📄 **[Tài liệu Phân tích Thiết kế, Kiến trúc & Kịch bản Kiểm thử (`../tailieu.md`)](../tailieu.md)**
2. 🤖 **[Nhật ký & Minh chứng Sử dụng AI (`NhatkysudungAI.md`)](NhatkysudungAI.md)**
3. 🗺️ **[Bản đồ Mã nguồn (`codebase_map.md`)](codebase_map.md)**

---

## 🛠 HƯỚNG DẪN CHẠY NHANH
```bash
dotnet ef database update
dotnet run
```
- Địa chỉ truy cập: **`http://localhost:5013`**
- **Tài khoản Admin:** `admin` / Mật khẩu: `123`
- **Tài khoản Kế toán:** `ketoan` / Mật khẩu: `123`
- **Tài khoản Sinh viên:** `DTC001` (hoặc `DTC001` -> `DTC040`) / Mật khẩu: `123`
