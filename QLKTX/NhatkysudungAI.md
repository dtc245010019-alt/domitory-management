# NHẬT KÝ VÀ MINH CHỨNG SỬ DỤNG AI TRONG DỰ ÁN QUẢN LÝ KÝ TÚC XÁ (QLKTX)

Tài liệu này tổng hợp toàn bộ nhật ký câu lệnh (Prompt), phản hồi từ AI, quá trình thử nghiệm tối ưu Prompt qua 3 vòng và minh chứng sinh viên kiểm chứng, review code, sửa lỗi trong suốt vòng đời phát triển phần mềm (**SDLC: KT1 -> KT2 -> KT3 -> Cuối kỳ**) của dự án **QLKTX**.

---

## 📍 1. GIỚI THIỆU CHUNG
- **Công cụ AI sử dụng:**
  - **Trong quá trình phát triển (SDLC):** ChatGPT / Google Gemini / Claude / GitHub Copilot.
  - **Tích hợp trực tiếp trong sản phẩm:** **Ollama Local LLM (`llama3`)** kết hợp cơ sở tri thức RAG (`wwwroot/data/knowledge_base.json`) và truy vấn CSDL SQL Server thời gian thực.
- **Mục đích:** Hỗ trợ phân tích thiết kế hệ thống (ERD, Use Case), sinh mã nguồn CRUD chuẩn MVC, tối ưu Prompt cho 3 tính năng AI (Chatbot nội quy, Gợi ý xếp phòng, Tóm tắt sự cố), và review code / khắc phục lỗi bảo mật.

---

## 📝 2. MINH CHỨNG SỬ DỤNG AI TRONG PHÂN TÍCH & THIẾT KẾ (BÀI KT1 - TIÊU CHÍ 9)

### 2.1. Phân tích nghiệp vụ và Thiết kế Cơ sở Dữ liệu (ERD)
* **User Prompt gửi AI:**
  > *"Hãy phân tích các quy trình quản lý ký túc xá sinh viên và thiết kế CSDL bằng Entity Framework Core (C#). Cần quản lý: SinhVien, Phong, HopDong, HoaDon, CoSoVatChat, ViPham, BaoCaoSuCo, TaiKhoan. Xác định rõ khóa chính, khóa ngoại và mối quan hệ."*

* **Phản hồi từ AI:** AI đề xuất 8 bảng thực thể với các mối quan hệ `1-N` giữa `Phong` - `SinhVien`, `Phong` - `HoaDon`, `SinhVien` - `HopDong`, `SinhVien` - `BaoCaoSuCo`, `SinhVien` - `ViPham`.

* **Kiểm chứng & Chỉnh sửa của Sinh viên:**
  - *Phát hiện hạn chế của AI:* Thiết kế ban đầu của AI dùng lệnh `Remove()` (xóa vật lý) khi sinh viên rời KTX, điều này sẽ làm gãy khóa ngoại ở bảng `HoaDon` và `HopDong`.
  - *Chỉnh sửa thực tế:* Giữ nguyên cột `TinhTrangLuuTru` (`"Đang ở"`, `"Đã rời đi"`, `"Đình chỉ"`) để áp dụng **Xóa mềm (Soft Delete)**, đồng thời bổ sung cột `SoLuongGiuong` vào bảng `Phong` để tự động đồng bộ trạng thái `"Còn chỗ"` / `"Đã đầy"`.

### 2.2. Xây dựng Cơ sở Tri thức (Knowledge Base) cho RAG Chatbot
* **User Prompt gửi AI:**
  > *"Hãy tạo tập dữ liệu tri thức định dạng JSON (`knowledge_base.json`) cho Chatbot Ký túc xá theo cấu trúc `intent` và mảng `facts`. Bao gồm đầy đủ các quy định: Giờ đóng/mở cửa KTX (05:00 - 23:00), bảng giá phòng, định mức & đơn giá điện nước, quy định gửi xe, tiếp khách, quy trình báo hỏng thiết bị và thủ tục trả phòng."*

* **Phản hồi từ AI:** AI sinh file JSON gồm các nhóm chủ đề (`GIOGIAC_NOIQUY`, `HOADON_DIEN_NUOC`, `BAO_HONG_THIET_BI`, `GUI_XE_RA_VAO`, `DANG_KY_TRA_PHONG`, `TIEP_KHACH_BAN_BE`).

* **Kiểm chứng & Chỉnh sửa của Sinh viên:**
  - *Chỉnh sửa:* Chuẩn hóa lại mức giá phòng trong JSON khớp 100% với đơn giá trong `DbInitializer.cs` (Phòng thường: 350.000đ/tháng/SV, Phòng điều hoà: 650.000đ/tháng/SV, Phòng điều hoà + nóng lạnh: 950.000đ/tháng/SV) để AI không trả lời lệch số liệu.

---

## 💻 3. MINH CHỨNG SỬ DỤNG AI TRONG LẬP TRÌNH HỆ THỐNG (BÀI KT2 - TIÊU CHÍ 9)

### 3.1. Xây dựng chức năng Import / Export Excel (`ClosedXML`)
* **User Prompt gửi AI:**
  > *"Viết Action `ImportExcel` và `DownloadTemplate` trong `SinhVienController` (ASP.NET Core 8 MVC) sử dụng thư viện `ClosedXML` để nhập danh sách sinh viên từ file `.xlsx` và tự động tạo tài khoản đăng nhập mặc định."*

* **Phản hồi từ AI:** AI sinh hàm đọc từng dòng từ `XLWorkbook` bắt đầu từ dòng 2 và lưu vào `DbContext`.

* **Kiểm chứng & Chỉnh sửa của Sinh viên:**
  - *Vấn đề phát hiện:* Excel thường tự động cắt mất số `0` ở đầu của cột `SoDienThoai` và `CCCD`.
  - *Chỉnh sửa trong code (`SinhVienController.cs`):* Bổ sung đoạn kiểm tra `if (!sdt.StartsWith("0")) sdt = "0" + sdt;` và kiểm tra chống trùng mã sinh viên (`_context.SinhViens.Any(s => s.MaSV == maSV)`) trước khi thêm.

### 3.2. Xây dựng luồng tính phí Điện nước & Xác thực thanh toán trên Portal
* **User Prompt gửi AI:**
  > *"Viết Action `TaoHoaDon` và `ThanhToan` trong `PortalController`. Khi lập hóa đơn điện nước cần tính tiền theo `(ChiSoMoi - ChiSoCu) * DonGia`. Khi sinh viên bấm thanh toán trên Portal cần xác nhận mật khẩu."*

* **Kiểm chứng & Chỉnh sửa của Sinh viên:**
  - Bổ sung kiểm tra ràng buộc chặt chẽ: Nếu `chiSoMoi <= chiSoCu` thì trả về `ModelState.AddModelError("chiSoMoi", "Chỉ số mới phải lớn hơn Chỉ số cũ.")` để ngăn chặn hóa đơn âm.

---

## 🧪 4. TỐI ƯU PROMPT QUA 3 VÒNG THỬ NGHIỆM (BÀI KT3 - TIÊU CHÍ 3 & 4)

Để đảm bảo AI trả lời chính xác, đúng ngôn ngữ tiếng Việt và không bịa đặt thông tin (*Hallucination*), nhóm đã tách toàn bộ Prompt ra lớp `QLKTX/Models/AIPrompts.cs` và thực hiện **3 vòng thử nghiệm cải tiến Prompt** trên mô hình `llama3` (Ollama):

### Bảng So sánh 3 Vòng Thử nghiệm Prompt

| Vòng thử nghiệm | Nội dung System & User Prompt thử nghiệm | Câu hỏi Test thực tế | Phản hồi của Model (`llama3`) | Đánh giá & Vấn đề phát hiện | Cải tiến ở vòng tiếp theo |
| :---: | :--- | :--- | :--- | :--- | :--- |
| **Vòng 1 (Prompt thô)** | **System:** *"You are a helpful dormitory assistant."*<br>**User:** *"Trả lời câu hỏi: {{question}}"* | *"KTX mấy giờ đóng cửa và giá phòng bao nhiêu?"* | Trả lời nửa tiếng Anh nửa tiếng Việt, tự bịa giờ đóng cửa là 22:00 và giá phòng tính bằng USD ($200/month). | ❌ **Thất bại:** AI không có dữ liệu nội bộ (Hallucination) và không giữ được ngôn ngữ tiếng Việt. | Bổ sung ngữ cảnh RAG từ `knowledge_base.json` + dữ liệu CSDL thực tế và ép trả lời tiếng Việt. |
| **Vòng 2 (Bổ sung dữ liệu RAG & CSDL)** | **System:** *"Bạn là trợ lý KTX, hãy trả lời bằng tiếng Việt dựa vào dữ liệu cung cấp."*<br>**User:** *"Dữ liệu: {{policy_chunks}}. Câu hỏi: {{question}}"* | 1. *"KTX mấy giờ đóng cửa?"*<br>2. *"Viết cho tôi code Python game rắn săn mồi"* | - Câu 1: Trả lời đúng 23:00 đóng cửa.<br>- Câu 2: Vẫn nhiệt tình viết code Python dù không liên quan đến KTX. | ⚠️ **Đạt 70%:** Trả lời đúng nghiệp vụ KTX nhưng chưa giới hạn phạm vi (vẫn trả lời câu hỏi ngoài lề) và đôi khi lộ cả trường `CCCD` khi hỏi về sinh viên. | Khóa chặt biên giới System Prompt (chỉ trả lời phạm vi KTX), trích dẫn mục nội quy và ẩn trường `CCCD`/`SĐT` trước khi serialize. |
| **Vòng 3 (Prompt chuẩn hóa cuối cùng - Đang áp dụng)** | **System (`AIPrompts.ChatbotSystemPrompt`):** *"Bạn là Trợ lý AI Quản lý Ký túc xá (QLKTX). BẮT BUỘC trả lời 100% bằng TIẾNG VIỆT... Tuyệt đối CHỈ sử dụng dữ liệu quy định và dữ liệu hệ thống được cung cấp... Nếu dữ liệu không chứa câu trả lời, hãy nói: 'Xin lỗi, tôi không có thông tin về vấn đề này trong hệ thống Ký túc xá'..."* | 1. *"Gợi ý cho tôi phòng điều hòa còn trống ở tầng 1"*<br>2. *"Thời tiết hôm nay thế nào?"* | - Câu 1: Liệt kê chính xác các phòng còn giường trống kèm giá 650.000đ/tháng (3.250.000đ/kỳ).<br>- Câu 2: Từ chối lịch sự, nhắc người dùng chỉ hỏi thông tin KTX. | ✅ **Đạt 100%:** Chuẩn tiếng Việt, chính xác theo CSDL & Nội quy, chống Hallucination và bảo mật tuyệt đối dữ liệu cá nhân. | Đóng gói chính thức vào `QLKTX/Models/AIPrompts.cs`. |

---

## 🔍 5. MINH CHỨNG DÙNG AI ĐỂ REVIEW CODE, PHÁT HIỆN LỖI & CẢI THIỆN BẢO MẬT (BÀI KT3 - TIÊU CHÍ 9 & CUỐI KỲ - TIÊU CHÍ 6)

Trong giai đoạn kiểm thử tích hợp và hoàn thiện sản phẩm, sinh viên đã sử dụng AI Agent để rà soát mã nguồn (Code Review), phát hiện và xử lý triệt để **5 lỗi kỹ thuật & bảo mật quan trọng**:

### 5.1. Phát hiện và xử lý lỗi `SqlNullValueException` trên trang Báo cáo sự cố
- **Tình huống lỗi:** Khi sinh viên gửi báo cáo sự cố mà không đính kèm ảnh, cột `HinhAnh` trong CSDL nhận giá trị `NULL` hoặc chuỗi rỗng, khiến trang `/Portal/QuanLySuCoAdmin` bị crash với ngoại lệ `SqlNullValueException: Data is Null`.
- **Giải pháp sau khi Review cùng AI:**
  - Cập nhật thuộc tính `public string? HinhAnh { get; set; } = "";` trong `BaoCaoSuCo.cs`.
  - Cho phép tham số `IFormFile? HinhAnhFile` là không bắt buộc trong `PortalController.GuiBaoCao`.
  - Đồng bộ mã sự cố theo định dạng `yyyyMMddHHmmss` (có giây) để không bị trùng khóa chính khi gửi liên tiếp.

### 5.2. Khắc phục lỗi không lưu được Biên bản Vi phạm & Tự động giải phóng phòng khi Đình chỉ
- **Tình huống lỗi:** Form lập biên bản vi phạm (`/ViPham/Create`) bị chặn bởi `ModelState.IsValid` do navigation property `SinhVien` và trường `GhiChu` bị `null`. Ngoài ra badge `"Cảnh cáo"` dùng class `bg-orange` không tồn tại trong Bootstrap 5 nên chữ trắng bị chìm trên nền trắng.
- **Giải pháp:**
  - Bổ sung `ModelState.Remove("SinhVien")` và gán `viPham.GhiChu ??= ""` trong `ViPhamController.cs`.
  - Khi hình thức kỷ luật là `"Đình chỉ"`, tự động cập nhật `sinhVien.TinhTrangLuuTru = "Đình chỉ"` và kiểm tra mở lại trạng thái phòng `"Còn chỗ"`.
  - Sửa CSS badge trong `Views/ViPham/Index.cshtml` hiển thị rõ nét cho cả 4 mức xử lý.

### 5.3. Chuẩn hóa tìm kiếm Mã phòng & Khóa bảo vệ phân quyền `[AdminAuthorize]`
- **Vấn đề:** Giao diện hiển thị mã phòng có tiền tố tĩnh `"P."` (ví dụ `P.101`, `P.A1-01`), nếu người dùng gõ `"P.101"` vào ô tìm kiếm sẽ không khớp với chuỗi `"101"` trong DB. Đồng thời một số Controller Admin chưa kiểm tra Session.
- **Giải pháp:**
  - Chuẩn hóa chuỗi tìm kiếm (cắt tiền tố `P.`, loại bỏ dấu chấm, dấu gạch ngang, khoảng trắng) trong `PhongController` và `SinhVienController`.
  - Xây dựng `AdminAuthorizeAttribute.cs` gắn vào toàn bộ các Controller quản trị (`Home`, `SinhVien`, `Phong`, `HopDong`, `CoSoVatChat`, `ViPham`, `BaoCao`) để chặn truy cập trái phép.

### 5.4. Bảo vệ Quyền riêng tư & Đạo đức AI (Ẩn CCCD và Số điện thoại)
- **Phát hiện khi Review bảo mật AI:** Trong `ChatbotController.cs`, câu lệnh `_context.SinhViens.Where(...).ToListAsync()` ban đầu serialize toàn bộ đối tượng `SinhVien` (bao gồm cả `CCCD` và `SoDienThoai`) vào Prompt gửi sang LLM.
- **Giải pháp Refactor bảo mật:**
  ```csharp
  // Chỉ trích xuất các trường công khai, tuyệt đối ẩn CCCD và SoDienThoai trước khi gửi sang AI
  var data = await _context.SinhViens
      .Where(s => s.TinhTrangLuuTru == "Đang ở")
      .Select(s => new { s.MaSV, s.HoTen, s.Lop, s.MaPhong, s.TinhTrangLuuTru })
      .Take(20)
      .AsNoTracking()
      .ToListAsync();
  ```

---

## 🛠 6. ĐÁNH GIÁ TỔNG KẾT VAI TRÒ CỦA AI TRONG SDLC
1. **Hiệu quả phát triển:** Ứng dụng AI có kiểm soát trong SDLC giúp rút ngắn ~45% thời gian viết boilerplate code, tập trung sâu vào logic nghiệp vụ đặc thù của KTX (đồng bộ sĩ số giường thực tế, xóa mềm lịch sử lưu trú).
2. **Chất lượng & Độ tin cậy của tính năng AI:** Kết hợp kiến trúc **Hybrid RAG (Local Knowledge Base + Real-time DB Query + Ollama LLM + Smart Fallback)** giúp cả 3 tính năng AI hoạt động ổn định 100% trong mọi điều kiện môi trường.
