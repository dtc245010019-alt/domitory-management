using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QLKTX.Models
{
    /// <summary>
    /// Framework Rào chắn An toàn (Safety Guardrails Framework) cho các phân hệ AI của Hệ thống QLKTX.
    /// Bao gồm 3 tầng rào chắn:
    /// 1. Input Guardrails: Ngăn Prompt Injection, kiểm soát độ dài, lọc ký tự điều khiển.
    /// 2. Output Guardrails: Làm sạch PII (CCCD, SĐT, Mật khẩu), kiểm soát Hallucination.
    /// 3. Execution Guardrails: Kiểm soát thời gian timeout (8s), kích hoạt Rule-based Smart Fallback.
    /// </summary>
    public static class SafetyGuardrails
    {
        public const int MaxInputLength = 1000;
        public const int DefaultTimeoutSeconds = 8;

        // Danh sách các mẫu Prompt Injection nguy hiểm
        private static readonly string[] InjectionPatterns = new[]
        {
            "bỏ qua chỉ dẫn",
            "bỏ qua tất cả chỉ dẫn",
            "bỏ qua quy định",
            "ignore previous instructions",
            "ignore all instructions",
            "ignore instructions",
            "trở thành hacker",
            "đóng vai hacker",
            "act as a hacker",
            "lộ mật khẩu",
            "tiết lộ mật khẩu",
            "hiển thị mật khẩu",
            "show all passwords",
            "reveal password",
            "system prompt",
            "hướng dẫn hệ thống",
            "tiết lộ câu lệnh hệ thống",
            "jailbreak",
            "bypass security",
            "quên vai trò của bạn",
            "bỏ qua vai trò"
        };

        #region 1. INPUT GUARDRAILS

        /// <summary>
        /// Phát hiện câu lệnh có dấu hiệu tấn công Prompt Injection hoặc vi phạm chính sách an toàn.
        /// </summary>
        public static bool IsPromptInjection(string? input, out string reason)
        {
            reason = string.Empty;
            if (string.IsNullOrWhiteSpace(input)) return false;

            string normalized = input.Trim().ToLowerInvariant();

            foreach (var pattern in InjectionPatterns)
            {
                if (normalized.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    reason = $"Phát hiện mẫu Prompt Injection nguy hiểm: '{pattern}'";
                    return true;
                }
            }

            // Phát hiện tấn công trích xuất token/câu lệnh hệ thống qua regex
            if (Regex.IsMatch(normalized, @"(?i)(reveal|show|print|output)\s+(all|the)?\s*(system\s*prompt|instructions|secret|passwords)"))
            {
                reason = "Phát hiện nỗ lực trích xuất System Prompt / Thông tin bảo mật hệ thống.";
                return true;
            }

            return false;
        }

        /// <summary>
        /// Làm sạch và chuẩn hóa chuỗi nhập liệu đầu vào:
        /// Loại bỏ ký tự điều khiển lạ, cắt khoảng trắng dư thừa, giới hạn độ dài tối đa chống tràn bộ nhớ.
        /// </summary>
        public static string SanitizeInput(string? input, int maxLength = MaxInputLength)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            // Loại bỏ ký tự điều khiển lạ (control characters) trừ \r, \n, \t
            string cleaned = Regex.Replace(input, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", string.Empty);

            cleaned = cleaned.Trim();
            if (cleaned.Length > maxLength)
            {
                cleaned = cleaned.Substring(0, maxLength);
            }

            return cleaned;
        }

        /// <summary>
        /// Kiểm định đầu vào toàn diện cho Chatbot / Prompt.
        /// </summary>
        public static bool ValidateInput(string? input, out string sanitized, out string? errorMessage)
        {
            errorMessage = null;
            sanitized = SanitizeInput(input);

            if (string.IsNullOrWhiteSpace(sanitized))
            {
                errorMessage = "Nội dung câu hỏi không được để trống.";
                return false;
            }

            if (IsPromptInjection(sanitized, out string reason))
            {
                errorMessage = "⚠️ Cảnh báo an toàn (Safety Guardrail): Câu lệnh của bạn vi phạm rào chắn an toàn hệ thống (phát hiện dấu hiệu can thiệp chỉ dẫn hoặc bảo mật). Vui lòng chỉ đặt câu hỏi liên quan đến nội quy, nghiệp vụ và đời sống Ký túc xá.";
                return false;
            }

            return true;
        }

        #endregion

        #region 2. OUTPUT GUARDRAILS & PRIVACY

        /// <summary>
        /// Che mờ số điện thoại để bảo vệ quyền riêng tư cá nhân theo chuẩn:
        /// Giữ lại 3 số đầu và 3 số cuối, che 4 số giữa bằng **** (Ví dụ: 0981234567 -> 098****567).
        /// </summary>
        public static string MaskPhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return string.Empty;

            string clean = phone.Trim().Replace(" ", "").Replace("-", "").Replace(".", "");
            if (clean.StartsWith("+84"))
            {
                clean = "0" + clean.Substring(3);
            }

            if (clean.Length >= 9)
            {
                int prefixLen = 3;
                int suffixLen = 3;
                if (clean.Length > prefixLen + suffixLen)
                {
                    return clean.Substring(0, prefixLen) + "****" + clean.Substring(clean.Length - suffixLen);
                }
            }
            else if (clean.Length >= 7)
            {
                return clean.Substring(0, 2) + "****" + clean.Substring(clean.Length - 2);
            }

            return "****";
        }

        /// <summary>
        /// PII Scrubber: Tự động phát hiện và che mờ thông tin định danh cá nhân nhạy cảm trong phản hồi AI:
        /// - CCCD / Số định danh cá nhân 12 chữ số
        /// - Số điện thoại liên lạc
        /// - Mật khẩu và thông tin xác thực
        /// </summary>
        public static string ScrubPII(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            string result = text;

            // 1. Quét và che CCCD (12 chữ số liên tiếp)
            result = Regex.Replace(result, @"\b\d{12}\b", "[ĐÃ_ẨN_CCCD]");
            result = Regex.Replace(result, @"(?i)(cccd|căn cước|cmnd)[\s:]*([0-9]{9,12})", "$1: [ĐÃ_ẨN_CCCD]");

            // 2. Quét và che số điện thoại (10 hoặc 11 chữ số bắt đầu bằng 0)
            result = Regex.Replace(result, @"\b(0\d{2})\d{3,4}(\d{3})\b", "$1****$2");

            // 3. Quét và che mật khẩu tài khoản
            result = Regex.Replace(result, @"(?i)(mật khẩu|matkhau|password)[\s:=]+([^\s,;]+)", "$1: [ĐÃ_BẢO_MẬT]");

            return result;
        }

        /// <summary>
        /// Bộ lọc làm sạch ngữ cảnh cung cấp cho AI: Loại bỏ hoàn toàn trường CCCD và Mật khẩu
        /// trước khi đưa vào System Prompt hoặc User Prompt.
        /// </summary>
        public static string FilterAiContext(string? rawContext)
        {
            if (string.IsNullOrWhiteSpace(rawContext)) return string.Empty;

            string result = rawContext;

            // Loại bỏ hoàn toàn các dòng hoặc cụm chứa CCCD
            result = Regex.Replace(result, @"(?i)(CCCD|Căn cước công dân|Số CMND)[\s:=]*[^\r\n,;]+[,;]?", string.Empty);

            // Loại bỏ hoàn toàn các trường chứa Mật khẩu
            result = Regex.Replace(result, @"(?i)(Mật khẩu|MatKhau|Password)[\s:=]*[^\r\n,;]+[,;]?", string.Empty);

            // Xóa bỏ các số định danh 12 chữ số còn sót lại
            result = Regex.Replace(result, @"\b\d{12}\b", "[ĐÃ_BẢO_MẬT_CCCD]");

            return result.Trim();
        }

        /// <summary>
        /// Tạo ngữ cảnh an toàn cho đối tượng Sinh viên (không chứa CCCD và Mật khẩu).
        /// </summary>
        public static object BuildSafeStudentContext(SinhVien? sv)
        {
            if (sv == null) return new { };

            return new
            {
                sv.MaSV,
                sv.HoTen,
                sv.NgaySinh,
                sv.Lop,
                SoDienThoai = MaskPhone(sv.SoDienThoai),
                sv.MaPhong,
                sv.TinhTrangLuuTru
            };
        }

        /// <summary>
        /// Kiểm tra rào chắn Hallucination: Cảnh báo hoặc làm sạch nếu câu trả lời chứa link ngoại lai hoặc ngoài KTX.
        /// </summary>
        public static string SanitizeHallucinations(string? aiResponse)
        {
            if (string.IsNullOrWhiteSpace(aiResponse)) return string.Empty;

            // Loại bỏ link markdown giả lập dạng [text](http://...) không thuộc phạm vi KTX
            string sanitized = Regex.Replace(aiResponse, @"\[([^\]]+)\]\((https?:\/\/[^\)]+)\)", "$1");

            return sanitized;
        }

        #endregion

        #region 3. EXECUTION GUARDRAILS & SMART FALLBACK

        /// <summary>
        /// Cung cấp câu trả lời dự phòng thông minh (Rule-based Smart Fallback) bằng tiếng Việt
        /// khi dịch vụ AI Ollama offline hoặc bị quá thời gian phản hồi (Timeout > 8s).
        /// </summary>
        public static string GetSmartFallback(string? topicOrIntent, string? userQuestion = null)
        {
            string intent = (topicOrIntent ?? string.Empty).ToUpperInvariant();
            string q = (userQuestion ?? string.Empty).ToLowerInvariant();

            if (intent.Contains("DIEN_NUOC") || intent.Contains("HOADON") || q.Contains("điện") || q.Contains("nước") || q.Contains("hóa đơn"))
            {
                return "💡 **Thông tin Hóa đơn & Điện nước KTX:**\n" +
                       "- Đơn giá điện sinh hoạt: **2.500 VNĐ / kWh**.\n" +
                       "- Đơn giá nước sinh hoạt: **12.000 VNĐ / m³**.\n" +
                       "- Hóa đơn được chốt vào ngày 25 hàng tháng. Sinh viên tra cứu tại mục 'Hóa đơn cá nhân' trên Cổng sinh viên.\n" +
                       "- Khi thanh toán trực tuyến, hệ thống yêu cầu xác thực mật khẩu cá nhân để đảm bảo an toàn tài chính.";
            }

            if (intent.Contains("PHONG") || q.Contains("phòng") || q.Contains("chuyển phòng") || q.Contains("giá phòng"))
            {
                return "💡 **Quy định Phòng ở & Xếp phòng KTX:**\n" +
                       "- Phòng tiêu chuẩn 4 giường: **650.000 VNĐ / sinh viên / tháng**.\n" +
                       "- Phòng chất lượng cao 2 giường (có máy lạnh): **1.200.000 VNĐ / sinh viên / tháng**.\n" +
                       "- Sinh viên muốn chuyển phòng cần nộp yêu cầu trên hệ thống trước ít nhất 5 ngày để BQL kiểm tra sĩ số phòng trống.";
            }

            if (intent.Contains("SU_CO") || q.Contains("sự cố") || q.Contains("hỏng") || q.Contains("sửa"))
            {
                return "💡 **Quy trình Tiếp nhận Báo cáo Sự cố & Sửa chữa:**\n" +
                       "- Sinh viên báo cáo sự cố qua mục 'Báo cáo sự cố' trên Cổng sinh viên kèm ảnh chụp hiện trạng.\n" +
                       "- Sự cố khẩn cấp (chập điện, vỡ ống nước): Ban Quản lý sẽ có mặt xử lý trong vòng 30 phút.\n" +
                       "- Sự cố thông thường (bóng đèn cháy, hỏng quạt): Xử lý trong vòng 24 - 48 giờ làm việc.";
            }

            if (intent.Contains("NOI_QUY") || intent.Contains("GIO_GIAC") || q.Contains("giờ") || q.Contains("nội quy") || q.Contains("cửa"))
            {
                return "💡 **Nội quy Giờ giấc & Ra vào KTX:**\n" +
                       "- Giờ mở cửa: **05:30 sáng** — Giờ đóng cửa: **23:00 tối** hàng ngày.\n" +
                       "- Sinh viên về muộn vì lý do học tập hoặc công việc cần xuất trình thẻ sinh viên và đăng ký sổ trực tại cổng bảo vệ.\n" +
                       "- Tuyệt đối nghiêm cấm nấu ăn bằng bếp gas, tàng trữ chất cấm hoặc cho người ngoài lưu trú qua đêm khi chưa được phép.";
            }

            return "💡 **Hỗ trợ Hệ thống Ký túc xá QLKTX:**\n" +
                   "Hiện tại dịch vụ phân tích ngôn ngữ nâng cao đang tạm thời bận hoặc bảo trì. Hệ thống đã kích hoạt chế độ dự phòng nội bộ.\n" +
                   "- Để tra cứu hóa đơn, phòng ở hoặc hợp đồng, bạn vui lòng sử dụng các menu chuyên biệt trên Cổng thông tin.\n" +
                   "- Hoặc liên hệ trực tiếp Văn phòng Ban Quản lý KTX tại tầng 1 nhà A để được hỗ trợ trực tiếp.";
        }

        #endregion
    }
}
