# NHẬT KÝ SỬ DỤNG AI TRONG DỰ ÁN QUẢN LÝ KÝ TÚC XÁ (QLKTX)

**Học phần:** Ứng dụng Trí tuệ Nhân tạo (AIA331) | **Mã đề tài:** 80309
**Nhóm 06:** Nguyễn Khánh Duy, Vũ Văn Dũng, Dương Văn Chung | **Trường:** ĐH Công nghệ Thông tin & Truyền thông - ĐH Thái Nguyên

Tài liệu ghi lại các prompt nhóm đã dùng, phản hồi của AI và phần nhóm kiểm chứng, chỉnh sửa qua các giai đoạn: KT1 → KT2 → KT3 → Cuối kỳ.

---

## 1. GIỚI THIỆU CHUNG
- **Công cụ AI dùng khi phát triển:** ChatGPT, Google Gemini, Claude, GitHub Copilot.
- **AI tích hợp trong sản phẩm:** Ollama chạy cục bộ (model `llama3`), kết hợp cơ sở tri thức `wwwroot/data/knowledge_base.json` và truy vấn CSDL SQL Server.
- **Mục đích:** hỗ trợ phân tích thiết kế (ERD, Use Case), sinh mã CRUD theo MVC, tối ưu prompt cho 3 chức năng AI (chatbot, gợi ý xếp phòng, tóm tắt sự cố) và review code.

---

## 2. GIAI ĐOẠN 1 (KT1): PHÂN TÍCH & THIẾT KẾ

### 2.1. Phân tích nghiệp vụ và thiết kế CSDL (ERD)
- **Prompt gửi AI:**
  > *"Hãy phân tích các quy trình quản lý ký túc xá sinh viên và thiết kế CSDL bằng Entity Framework Core (C#). Cần quản lý: SinhVien, Phong, HopDong, HoaDon, CoSoVatChat, ViPham, BaoCaoSuCo, TaiKhoan. Xác định rõ khóa chính, khóa ngoại và mối quan hệ."*
- **Phản hồi của AI:** đề xuất 8 bảng với các quan hệ 1-N giữa `Phong` - `SinhVien`, `Phong` - `HoaDon`, `SinhVien` - `HopDong`, `SinhVien` - `BaoCaoSuCo`, `SinhVien` - `ViPham`.
- **Nhóm kiểm chứng và chỉnh sửa:**
  - AI đề xuất xóa vật lý sinh viên (`Remove()` / `DELETE FROM SinhViens`) khi sinh viên rời KTX. Cách này làm gãy khóa ngoại ở `HoaDon`, `HopDong` và mất lịch sử tài chính.
  - Nhóm dùng xóa mềm: giữ cột `TinhTrangLuuTru` (`"Đang ở"`, `"Đã rời đi"`, `"Đình chỉ"`), đặt `DeleteBehavior.Restrict` cho mọi khóa ngoại, và dùng `SoLuongGiuong` của bảng `Phong` để đồng bộ trạng thái `"Còn chỗ"` / `"Đã đầy"`.

### 2.2. Xây dựng cơ sở tri thức cho chatbot
- **Prompt gửi AI:**
  > *"Hãy tạo tập dữ liệu tri thức định dạng JSON (`knowledge_base.json`) cho Chatbot Ký túc xá theo cấu trúc `intent` và mảng `facts`. Bao gồm: giờ đóng/mở cửa KTX (05:00 - 23:00), bảng giá phòng, đơn giá điện nước, quy định gửi xe, tiếp khách, quy trình báo hỏng thiết bị và thủ tục trả phòng."*
- **Phản hồi của AI:** file JSON gồm các nhóm `GIOGIAC_NOIQUY`, `HOADON_DIEN_NUOC`, `BAO_HONG_THIET_BI`, `GUI_XE_RA_VAO`, `DANG_KY_TRA_PHONG`, `TIEP_KHACH_BAN_BE`.
- **Nhóm chỉnh sửa:** sửa giá phòng trong JSON cho khớp với `DbInitializer.cs` (phòng thường 350.000đ, phòng điều hòa 650.000đ, phòng điều hòa + nóng lạnh 950.000đ / tháng / sinh viên) để chatbot không trả lời lệch số liệu.

---

## 3. GIAI ĐOẠN 2 (KT2): LẬP TRÌNH & SỬA LỖI

### 3.1. Nhập / xuất Excel (`ClosedXML`)
- **Prompt gửi AI:**
  > *"Viết Action `ImportExcel` và `DownloadTemplate` trong `SinhVienController` (ASP.NET Core 8 MVC) sử dụng thư viện `ClosedXML` để nhập danh sách sinh viên từ file `.xlsx` và tự động tạo tài khoản đăng nhập mặc định."*
- **Phản hồi của AI:** hàm đọc từng dòng của `XLWorkbook` từ dòng 2 rồi lưu vào `DbContext`.
- **Nhóm chỉnh sửa:** Excel tự bỏ số `0` đầu ở cột số điện thoại và CCCD, nên nhóm thêm `if (!sdt.StartsWith("0")) sdt = "0" + sdt;` (tương tự với CCCD) và kiểm tra trùng mã sinh viên trước khi thêm.

### 3.2. Tính tiền điện nước và xác thực thanh toán
- **Prompt gửi AI:**
  > *"Viết Action `TaoHoaDon` và `ThanhToan` trong `PortalController`. Khi lập hóa đơn điện nước cần tính tiền theo `(ChiSoMoi - ChiSoCu) * DonGia`. Khi sinh viên bấm thanh toán trên Portal cần xác nhận mật khẩu."*
- **Nhóm chỉnh sửa:** thêm ràng buộc `chiSoMoi <= chiSoCu` thì báo `ModelState.AddModelError("chiSoMoi", "Chỉ số mới phải lớn hơn Chỉ số cũ.")` để không tạo hóa đơn âm.

### 3.3. Lỗi sĩ số X/Y của phòng
- **Tình huống:** mã do AI sinh đếm mọi sinh viên có `MaPhong == id`, kể cả sinh viên đã rời đi. Phòng 4 giường đã có 2 người chuyển đi vẫn báo 4/4 ("Đã đầy").
- **Nhóm chỉnh sửa:** chỉ đếm sinh viên `TinhTrangLuuTru == "Đang ở"`; viết hàm `DongBoTrangThaiPhongAsync(maPhong)` để chuyển phòng về `"Còn chỗ"` khi sinh viên trả phòng hoặc bị đình chỉ.

---

## 4. GIAI ĐOẠN 3 (KT3): TỐI ƯU PROMPT QUA 3 VÒNG

Prompt được tách ra lớp `QLKTX/Models/AIPrompts.cs` và thử trên `llama3` qua 3 vòng:

| Vòng | Prompt thử nghiệm | Câu hỏi thử | Phản hồi của model | Đánh giá | Cải tiến cho vòng sau |
| :---: | :--- | :--- | :--- | :--- | :--- |
| **1** | **System:** *"You are a helpful dormitory assistant."*<br>**User:** *"Trả lời câu hỏi: {{question}}"* | *"KTX mấy giờ đóng cửa và giá phòng bao nhiêu?"* | Trả lời lẫn tiếng Anh, tự bịa giờ đóng cửa 22:00 và giá phòng $200/tháng. | Không đạt: model không có dữ liệu nội bộ nên bịa thông tin, không giữ tiếng Việt. | Đưa nội quy từ `knowledge_base.json` và dữ liệu CSDL vào prompt, bắt buộc trả lời tiếng Việt. |
| **2** | **System:** *"Bạn là trợ lý KTX, hãy trả lời bằng tiếng Việt dựa vào dữ liệu cung cấp."*<br>**User:** *"Dữ liệu: {{policy_chunks}}. Câu hỏi: {{question}}"* | 1. *"KTX mấy giờ đóng cửa?"*<br>2. *"Viết cho tôi code Python game rắn săn mồi"* | Câu 1 trả lời đúng 23:00. Câu 2 vẫn viết code Python. Khi hỏi về sinh viên đôi khi lộ CCCD. | Đạt một phần: đúng nội quy nhưng chưa giới hạn phạm vi và chưa bảo vệ dữ liệu cá nhân. | Giới hạn phạm vi trong system prompt, thêm lớp `SafetyGuardrails` (chặn prompt injection, che CCCD/SĐT). |
| **3** (đang dùng) | Tách 2 system prompt theo vai trò: `AdminChatbotSystemPrompt`, `StudentChatbotSystemPrompt` (*"BẮT BUỘC trả lời 100% bằng TIẾNG VIỆT… Chỉ sử dụng dữ liệu hệ thống được cung cấp…"*), kèm kiểm tra đầu vào/đầu ra của `SafetyGuardrails` | 1. *"Gợi ý cho tôi phòng điều hòa còn trống ở tầng 1"*<br>2. *"Thời tiết hôm nay thế nào?"* | Câu 1 liệt kê các phòng còn giường kèm giá 650.000đ/tháng. Câu 2 từ chối và nhắc chỉ hỏi về KTX. | Đạt với bộ câu hỏi thử. Câu tấn công diễn đạt khác danh sách chặn vẫn có thể lọt qua. | Giữ cấu hình này; thêm phương án dự phòng khi Ollama không chạy. |

---

## 5. REVIEW CODE BẰNG AI (KT3 & CUỐI KỲ)

### 5.1. Lỗi `SqlNullValueException` ở trang Quản lý sự cố
- **Tình huống:** sinh viên gửi báo cáo sự cố không kèm ảnh, cột `HinhAnh` nhận `NULL` khiến `/Portal/QuanLySuCoAdmin` lỗi `SqlNullValueException: Data is Null`.
- **Cách sửa:**
  - Khai báo `public string? HinhAnh { get; set; } = "";` trong `BaoCaoSuCo.cs`.
  - Cho tham số `IFormFile? HinhAnhFile` của `PortalController.GuiBaoCao` là không bắt buộc; chỉ lưu file khi `HinhAnhFile != null && HinhAnhFile.Length > 0`.
  - Tạo mã sự cố dạng `SC_yyMMddHHmm_<số ngẫu nhiên>` để không trùng khóa chính khi gửi liên tiếp.

### 5.2. Không lưu được biên bản vi phạm; giải phóng phòng khi đình chỉ
- **Tình huống:** form `/ViPham/Create` bị chặn bởi `ModelState.IsValid` vì navigation property `SinhVien` và trường `GhiChu` bị null. Badge "Cảnh cáo" dùng class `bg-orange` không có trong Bootstrap 5 nên chữ bị chìm.
- **Cách sửa:**
  - Thêm `ModelState.Remove("SinhVien")` và `viPham.GhiChu ??= ""` trong `ViPhamController.cs`.
  - Hình thức `"Đình chỉ"` thì cập nhật `TinhTrangLuuTru = "Đình chỉ"` và mở lại phòng `"Còn chỗ"` nếu còn giường.
  - Đổi badge sang `bg-warning text-dark` trong `Views/ViPham/Index.cshtml`.

### 5.3. Tìm kiếm mã phòng và phân quyền `[AdminAuthorize]`
- **Vấn đề:** giao diện hiển thị mã phòng có tiền tố `"P."` (`P.101`, `P.A1-01`) nên gõ `"P.101"` không khớp `"101"` trong DB; một số controller quản trị chưa kiểm tra session.
- **Cách sửa:**
  - Chuẩn hóa từ khóa (bỏ `P.`, dấu chấm, gạch ngang, khoảng trắng) trong `PhongController` và `SinhVienController`.
  - Viết `AdminAuthorizeAttribute.cs` gắn vào các controller quản trị; tham số `OnlyAdmin = true` chặn tài khoản Kế toán ở các chức năng kỹ thuật và kỷ luật.

### 5.4. Bảo vệ dữ liệu cá nhân trong chatbot
- **Phát hiện khi review:** nhánh tra cứu sinh viên của chatbot đưa nguyên số điện thoại và CCCD vào câu trả lời.
- **Cách sửa trong code hiện tại:**
  - Số điện thoại của sinh viên khác hiển thị qua `SafetyGuardrails.MaskPhone` (dạng `098****567`); sinh viên chỉ thấy đầy đủ số của chính mình.
  - Mọi câu trả lời của AI đi qua `SafetyGuardrails.ScrubPII` (thay chuỗi 12 chữ số bằng `[ĐÃ_ẨN_CCCD]`) và `SanitizeHallucinations` (bỏ liên kết giả).
  - Hàm `SafetyGuardrails.FilterAiContext` (loại CCCD, mật khẩu khỏi dữ liệu) đã có và được kiểm thử, nhưng chưa được gọi ở luồng chatbot; đây là việc cần làm tiếp.

### 5.5. Chatbot trả lời lạc đề với câu hỏi nội quy
- **Tình huống:** hỏi *"tôi muốn tìm hiểu về giờ đóng cửa và giá điện nước của ktx"* thì chatbot trả về báo cáo công nợ, vì bộ phân loại từ khóa gặp "điện nước" là xếp ngay vào nhánh hóa đơn mà không gọi AI.
- **Cách sửa:** tách câu hỏi nội quy (giá điện, giờ đóng cửa, thủ tục…) khỏi câu hỏi dữ liệu (của tôi, còn nợ, mã phòng…); câu hỏi nội quy được trả lời từ cơ sở tri thức và lấy đủ mọi chủ đề nhắc tới; câu vừa hỏi dữ liệu vừa hỏi nội quy thì trả dữ liệu kèm phần nội quy liên quan. Có 8 test trong `ChatbotRoutingTests.cs`.

---

## 6. KIỂM THỬ TỰ ĐỘNG

Lần chạy `dotnet test` gần nhất: **64/64 test case đạt** (tính cả các bộ dữ liệu của `[Theory]`):

| Tệp | Số test | Nội dung |
| :--- | :---: | :--- |
| `HoaDonCalculationsTests.cs` | 20 | Tính tiền điện nước, chặn chỉ số mới ≤ chỉ số cũ, tính tiền phòng |
| `PhongVaSiSoTests.cs` | 4 | Tăng/giảm sĩ số, giải phóng phòng khi xóa mềm hoặc đình chỉ |
| `AIFallbackAndPrivacyTests.cs` | 19 | Che số điện thoại, lọc CCCD, phương án dự phòng khi Ollama tắt, chặn prompt injection |
| `SecurityAndEndpointIntegrationTests.cs` | 10 | Endpoint `/health`, `/_diag`, chặn truy cập trang quản trị khi chưa đăng nhập |
| `AIAndBusinessIntegrationTests.cs` | 3 | Gợi ý xếp phòng, tóm tắt sự cố, thanh toán sai mật khẩu bị chặn |
| `ChatbotRoutingTests.cs` | 8 | Câu hỏi nội quy không bị trả về công nợ, câu hỏi nhiều ý, câu hỏi vừa dữ liệu vừa nội quy |

---

## 7. NHẬN XÉT VỀ VAI TRÒ CỦA AI
1. AI giúp viết nhanh phần mã lặp lại (CRUD, form, cấu hình), để nhóm tập trung vào các quy tắc nghiệp vụ riêng của KTX như đồng bộ sĩ số và xóa mềm.
2. Mã do AI sinh nhiều lần sai ở chính các quy tắc nghiệp vụ (xóa cứng, đếm sai sĩ số, null ảnh, lộ dữ liệu cá nhân), nên mọi đoạn mã đều phải được nhóm đọc lại, chạy thử và viết test trước khi dùng.
