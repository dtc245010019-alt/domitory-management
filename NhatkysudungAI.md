# NHẬT KÝ SỬ DỤNG AI CÓ KIỂM SOÁT TRONG QUY TRÌNH SDLC (AI USAGE LOG)
## DỰ ÁN: HỆ THỐNG QUẢN LÝ KÝ TÚC XÁ TÍCH HỢP TRÍ TUỆ NHÂN TẠO (QLKTX)
**Học phần:** Ứng dụng Trí tuệ Nhân tạo (AIA331) | **Mã đề tài:** 80309  
**Sinh viên thực hiện:** Nguyễn Khánh Duy | **Trường:** ĐH Công nghệ Thông tin & Truyền thông - ĐH Thái Nguyên  

---

## 1. GIAI ĐOẠN 1 (BÀI KIỂM TRA THƯỜNG XUYÊN 1): PHÂN TÍCH & THIẾT KẾ HỆ THỐNG

### 1.1. Prompt phân tích bài toán & Yêu cầu chức năng
- **Prompt gửi AI:**
  ```text
  Tôi đang làm đề tài 80309 - Hệ thống Quản lý Ký túc xá tích hợp AI (AIA331). Hãy giúp tôi liệt kê 8 chức năng quản lý cốt lõi, 3 chức năng AI và phân tích các trường dữ liệu cần thiết cho bảng SinhVien, Phong, HopDong, HoaDon, ViPham, BaoCaoSuCo, CoSoVatChat, TaiKhoan.
  ```
- **Phản hồi của AI:** AI đề xuất cấu trúc 8 bảng và luồng nghiệp vụ MVC.
- **Kiểm chứng & Chỉnh sửa của sinh viên:**
  - *Phát hiện thiếu sót:* AI đề xuất xóa cứng sinh viên (`DELETE FROM SinhViens`), điều này sẽ phá vỡ toàn vẹn khóa ngoại với bảng `HoaDon` và `HopDong`.
  - *Chỉnh sửa của sinh viên:* Bắt buộc áp dụng nguyên tắc **Xóa mềm (Soft Delete)**: Khi sinh viên trả phòng, chỉ cập nhật `TinhTrangLuuTru = "Đã rời đi"`, bảo toàn 100% lịch sử và các bảng liên kết.

---

## 2. GIAI ĐOẠN 2 (BÀI KIỂM TRA THƯỜNG XUYÊN 2): LẬP TRÌNH & XỬ LÝ LỖI PHÁT SINH

### 2.1. Lỗi 1: Xử lý Upload ảnh sự cố bị `SqlNullValueException`
- **Tình huống:** Khi sinh viên gửi phản ánh sự cố không đính kèm file ảnh, hàm upload do AI sinh ra gán giá trị `null` vào trường `HinhAnh` vốn không cho phép `null` trong CSDL.
- **Can thiệp của sinh viên:**
  - Kiểm tra điều kiện `if (HinhAnhFile != null && HinhAnhFile.Length > 0)` trước khi lưu file vào `wwwroot/uploads/suco/`.
  - Nếu không có ảnh, gán chuỗi rỗng `""` an toàn thay vì để `null`, ngăn chặn hoàn toàn ngoại lệ sập ứng dụng.

### 2.2. Lỗi 2: Sai lệch sĩ số thực tế X/Y của phòng
- **Tình huống:** AI sinh mã đếm tổng số bản ghi sinh viên trong phòng `context.SinhViens.Count(s => s.MaPhong == id)`. Khi sinh viên đã rời đi, sĩ số phòng vẫn đếm cả sinh viên cũ, dẫn đến phòng 4 người dù đã chuyển đi 2 người vẫn báo 4/4 ("Đã đầy").
- **Can thiệp của sinh viên:**
  - Bổ sung điều kiện lọc bất biến: Chỉ đếm những sinh viên có `TinhTrangLuuTru == "Đang ở"`.
  - Viết hàm `DongBoTrangThaiPhongAsync(maPhong)` tự động chuyển phòng về `"Còn chỗ"` ngay khi sinh viên trả phòng hoặc bị đình chỉ kỷ luật.

---

## 3. GIAI ĐOẠN 3 (BÀI KIỂM TRA THƯỜNG XUYÊN 3): TỐI ƯU HÓA PROMPT QUA 3 VÒNG THỬ NGHIỆM

| Vòng thử nghiệm | Cấu trúc Prompt | Vấn đề phát hiện | Giải pháp cải tiến |
| :---: | :--- | :--- | :--- |
| **Vòng 1 (Sơ khai)** | Prompt đơn giản: *"Bạn là trợ lý KTX, hãy trả lời câu hỏi của sinh viên: {câu_hỏi}"* | AI bị "ảo giác" (Hallucination), tự bịa giờ đóng cửa (báo 22:00 thay vì 23:00) và tự bịa link liên kết ngoài. | Cần bổ sung cơ sở tri thức chuẩn và nghiêm cấm tự bịa đặt. |
| **Vòng 2 (RAG Context)** | Ghép nội quy từ `knowledge_base.json`: *"Dưới đây là nội quy: {policy_chunks}. Trả lời câu hỏi: {câu_hỏi}"* | AI trả lời đúng nội quy nhưng khi người dùng hỏi các câu ngoài lề (viết code Python, thời tiết) AI vẫn trả lời lan man. | Cần giới hạn phạm vi chặt chẽ trong System Prompt. |
| **Vòng 3 (Guardrails & Fallback)** | System Prompt hoàn chỉnh trong `AIPrompts.cs` + Rào chắn an toàn `SafetyGuardrails.cs` | Giải quyết triệt để: Chống Prompt Injection, lọc sạch PII (CCCD, SĐT), khống chế Timeout 8s và tự động Fallback nội bộ. | **ĐẠT CHUẨN 100% PRODUCTION** |

---

## 4. GIAI ĐOẠN 4 (CUỐI KỲ): REVIEW BẢO MẬT & KIỂM THỬ TỰ ĐỘNG

### 4.1. Bảo vệ Quyền riêng tư Dữ liệu (AI Privacy & Ethics)
- **Rà soát:** Khi gửi dữ liệu cho mô hình ngôn ngữ lớn (Ollama), không được phép truyền CCCD 12 số, mật khẩu hoặc số điện thoại nguyên bản.
- **Hiện thực:** Tạo hàm `SafetyGuardrails.MaskPhone(phone)` (`098****567`) và `FilterAiContext()` làm sạch dữ liệu trước khi ghép vào Prompt.

### 4.2. Bộ kiểm thử tự động xUnit (53/53 Tests PASS)
- Viết 53 kịch bản kiểm thử đơn vị và tích hợp trong `QLKTX.Tests`:
  - `HoaDonCalculationsTests`: Chặn hóa đơn âm, tính tiền phòng theo loại phòng và số tháng.
  - `PhongVaSiSoTests`: Tăng giảm sĩ số, giải phóng phòng khi xóa mềm hoặc kỷ luật đình chỉ.
  - `AIFallbackAndPrivacyTests`: Che mờ số điện thoại, Smart Fallback tiếng Việt, chống Prompt Injection.
  - `SecurityAndEndpointIntegrationTests`: Endpoint `/health`, `/_diag`, phân quyền chặn Admin truy cập trái phép (Redirect 302).

