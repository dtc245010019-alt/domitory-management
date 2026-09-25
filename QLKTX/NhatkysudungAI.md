# NHẬT KÝ VÀ MINH CHỨNG SỬ DỤNG AI TRONG DỰ ÁN QUẢN LÝ KÝ TÚC XÁ (QLKTX)

Tài liệu này tổng hợp nhật ký các câu lệnh (Prompt), phản hồi từ AI và quá trình sinh viên kiểm tra, tinh chỉnh mã nguồn trong suốt quá trình xây dựng dự án **QLKTX**.

---

## 📍 1. GIỚI THIỆU CHUNG
- **Công cụ AI sử dụng**: ChatGPT / Google Gemini / GitHub Copilot.
- **Mục đích**: Hỗ trợ phân tích yêu cầu bài toán, thiết kế CSDL (ERD), sinh bộ dữ liệu tri thức mẫu (`data/knowledge_base.json`) và viết các hàm xử lý logic trong C# ASP.NET Core 8 MVC.

---

## 📝 2. SỬ DỤNG AI TRONG PHÂN TÍCH VÀ THIẾT KẾ HỆ THỐNG (BÀI KT1)

### 2.1. Thiết kế Cơ sở Dữ liệu (ERD)
* **User Prompt gửi AI**:
  > *"Hãy thiết kế CSDL cho hệ thống Quản lý Ký túc xá bằng Entity Framework Core (C#). Cần các bảng: SinhVien, Phong, HopDong, HoaDon, CoSoVatChat, ViPham, BaoCaoSuCo, TaiKhoan. Xác định rõ khóa chính, khóa ngoại và mối quan hệ."*

* **Phản hồi từ AI**: AI gợi ý danh sách các Class Entities với quan hệ 1-N giữa `Phong` - `SinhVien`, 1-N giữa `Phong` - `HoaDon`, 1-N giữa `SinhVien` - `BaoCaoSuCo`.

* **Đánh giá & Chỉnh sửa của Sinh viên**:
  - *Nhận xét*: Thiết kế ban đầu của AI chưa kiểm soát được số lượng sinh viên ở thực tế trong phòng.
  - *Chỉnh sửa*: Bổ sung thuộc tính `SoLuongGiuong` vào bảng `Phong` để viết logic ràng buộc khi thêm hợp đồng/xếp chỗ cho sinh viên.

---

### 2.2. Xây dựng Cơ sở Tri thức (Knowledge Base) cho AI Chatbot
* **User Prompt gửi AI**:
  > *"Hãy tạo cho tôi một tập dữ liệu câu hỏi - trả lời mẫu dạng JSON cho Chatbot Ký túc xá. Bao gồm các chủ đề: Giờ đóng cửa KTX (23:00), giá điện (2.000đ/kWh), giá nước (10.000đ/m3), quy trình báo cáo hỏng hóc thiết bị qua Portal và nội quy phòng ở."*

* **Phản hồi từ AI**: AI sinh tập dữ liệu dạng JSON chứa danh sách từ khóa (`keywords`) và nội dung phản hồi tương ứng (`answer`).

* **Đánh giá & Chỉnh sửa của Sinh viên**:
  - *Chỉnh sửa*: Thêm các từ khóa viết tắt, từ đồng nghĩa mà sinh viên thường nhập (vd: "dien", "nuoc", "gio dong cua", "su co", "hong quat") và lưu thành file `QLKTX/data/knowledge_base.json`.

---

## 💻 3. SỬ DỤNG AI TRONG LẬP TRÌNH & XỬ LÝ LOGIC (BÀI KT2)

### 3.1. Tạo hàm Xuất Danh sách Sinh viên ra Excel (`ClosedXML`)
* **User Prompt gửi AI**:
  > *"Viết code C# trong SinhVienController ASP.NET Core 8 MVC sử dụng thư viện ClosedXML để xuất danh sách SinhVien từ DbContext ra file Excel .xlsx."*

* **Phản hồi từ AI**: AI sinh hàm `ExportToExcel()` tạo `XLWorkbook`, tạo Header và duyệt danh sách dữ liệu.

* **Kiểm tra & Chỉnh sửa của Sinh viên**:
  - *Chỉnh sửa*: Thay đổi tiêu đề các cột trong file Excel sang tiếng Việt có dấu (`Mã SV`, `Họ và Tên`, `Tên Phòng`, `SĐT`, `Lớp`) và gọi hàm `AdjustToContents()` để tự động chỉnh độ rộng cột.

---

### 3.2. Lập trình `ChatbotController` & Giao diện Widget Chatbot
* **User Prompt gửi AI**:
  > *"Viết ChatbotController trong C# ASP.NET Core nhận câu hỏi từ Client qua AJAX POST, đọc file JSON `data/knowledge_base.json`, tìm kiếm câu trả lời khớp từ khóa nhất và trả về kết quả Json."*

* **Phản hồi từ AI**: AI viết hàm controller đọc file JSON bằng `System.Text.Json` và thực hiện so khớp chuỗi từ khóa.

* **Kiểm tra & Chỉnh sửa của Sinh viên**:
  - *Chỉnh sửa*: Thêm câu phản hồi mặc định khi không tìm thấy từ khóa khớp (*"Xin lỗi, em chưa hiểu câu hỏi. Bạn vui lòng liên hệ Ban quản lý KTX để được hỗ trợ trực tiếp!"*).
  - *Tích hợp*: Nhúng giao diện Chatbot Widget vào file `_StudentLayout.cshtml` để sinh viên có thể trò chuyện từ bất kỳ trang nào trên Portal.

---

### 3.3. Viết Class Khởi tạo Dữ liệu mẫu (`DbInitializer.cs`)
* **User Prompt gửi AI**:
  > *"Viết code C# cho class DbInitializer sử dụng Entity Framework Core để tự động tạo tài khoản Admin, tài khoản SinhVien và phòng ở mẫu nếu CSDL chưa có dữ liệu."*

* **Kết quả**: Tích hợp thành công vào `DbInitializer.cs` và gọi trong `Program.cs`.

---

## 🛠 4. KẾT LUẬN & ĐÁNH GIÁ
Việc ứng dụng AI giúp rút ngắn 40% thời gian thiết kế CSDL và viết code xử lý. Việc sử dụng mô hình **Local Knowledge-Base Chatbot** giúp hệ thống hoạt động hoàn toàn tự chủ, phản hồi mượt mà, không phụ thuộc vào kết nối mạng bên ngoài hay chi phí API Key.