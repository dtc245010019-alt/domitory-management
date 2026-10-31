using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using QLKTX.Controllers;
using QLKTX.Models;
using Xunit;

namespace QLKTX.Tests
{
    /// <summary>
    /// Kiểm thử định tuyến câu hỏi của Chatbot khi Ollama không chạy (kết quả xác định, không phụ thuộc LLM):
    /// câu hỏi nội quy/biểu phí không được trả về số liệu công nợ, câu gộp nhiều ý không bị mất ý.
    /// </summary>
    public class ChatbotRoutingTests
    {
        private const string FactGioDongCua = "Giờ đóng cửa KTX: 23:00";
        private const string FactGiaDien = "2.500 VNĐ / kWh";

        private static readonly ApplicationDbContext SharedContext = CreateContext();

        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("ChatbotRoutingTestDb")
                .Options;
            var context = new ApplicationDbContext(options);
            context.Database.EnsureCreated();
            DbInitializer.Initialize(context);
            return context;
        }

        private static string FindWebRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "QLKTX", "wwwroot")))
                dir = dir.Parent;
            return Path.Combine(dir!.FullName, "QLKTX", "wwwroot");
        }

        private sealed class OfflineHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => throw new HttpRequestException("Ollama offline (test)");
        }

        // Ollama giả luôn trả lời một chuỗi cố định, dùng để kiểm tra câu hỏi có bị gửi sang LLM hay không
        private sealed class AlwaysAnswerHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"message\":{\"content\":\"FAKE_LLM_ANSWER\"}}")
                });
        }

        private static async Task<JsonElement> AskAsync(string message, string role, bool ollamaOnline = false)
        {
            var env = new Mock<IWebHostEnvironment>();
            env.Setup(e => e.WebRootPath).Returns(FindWebRoot());
            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(() => new HttpClient(ollamaOnline ? new AlwaysAnswerHandler() : new OfflineHandler()));

            var controller = new ChatbotController(env.Object, SharedContext, httpFactory.Object,
                Mock.Of<ILogger<ChatbotController>>(), Mock.Of<IConfiguration>());

            var httpContext = new DefaultHttpContext { Session = new MockSession() };
            if (role == "Admin")
            {
                httpContext.Session.SetString("Admin_DangNhap", "true");
                httpContext.Session.SetString("VaiTro", "Admin");
            }
            else
            {
                httpContext.Session.SetString("MaSV_DangNhap", "DTC001");
                httpContext.Session.SetString("VaiTro", "SinhVien");
            }
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            var result = await controller.Ask(new ChatRequest { Message = message, Mode = "QA", Role = role });
            var json = Assert.IsType<JsonResult>(result);
            string raw = JsonSerializer.Serialize(json.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return JsonDocument.Parse(raw).RootElement.Clone();
        }

        private static string ResponseOf(JsonElement root) => root.GetProperty("response").GetString() ?? "";

        [Theory]
        [InlineData("Admin")]
        [InlineData("SinhVien")]
        public async Task CauHoiNoiQuyGopHaiY_TraLoiDuCaHaiY_KhongTraVeCongNo(string role)
        {
            var root = await AskAsync("tôi muốn tìm hiểu về giờ đóng cửa và giá điện nước của ktx", role);
            string response = ResponseOf(root);

            Assert.Contains(FactGioDongCua, response);
            Assert.Contains(FactGiaDien, response);
            Assert.DoesNotContain("CÔNG NỢ", response);
            Assert.DoesNotContain("Kết quả tra cứu Hóa đơn", response);
        }

        [Fact]
        public async Task CauHoiBaY_GioGiac_DienNuoc_GuiXe_DeuCoTrongCauTraLoi()
        {
            var root = await AskAsync("Cho mình hỏi KTX mấy giờ đóng cửa, giá nước bao nhiêu và phí gửi xe máy thế nào?", "SinhVien");
            string response = ResponseOf(root);

            Assert.Contains(FactGioDongCua, response);
            Assert.Contains("15.000 VNĐ / m³", response);
            Assert.Contains("80.000 VNĐ / tháng", response);
        }

        [Fact]
        public async Task ChoToiHoiGioDongCua_KhongBiHieuThanhBaoSuCo()
        {
            var root = await AskAsync("cho tôi hỏi giờ đóng cửa của ktx", "SinhVien");

            Assert.False(root.TryGetProperty("actionCard", out _));
            Assert.Contains(FactGioDongCua, ResponseOf(root));
        }

        [Fact]
        public async Task AdminHoiChoToiHoiNoiQuy_KhongBiChanNhuCauHoiCaNhan()
        {
            var root = await AskAsync("cho tôi hỏi nội quy giờ giấc ktx", "Admin");
            string response = ResponseOf(root);

            Assert.DoesNotContain("Thông báo quyền hạn Quản trị viên", response);
            Assert.Contains(FactGioDongCua, response);
        }

        [Fact]
        public async Task CauHoiVuaTraDuLieuVuaHoiNoiQuy_TraHoaDonKemNoiQuyLienQuan()
        {
            var root = await AskAsync("giờ đóng cửa là mấy giờ và hóa đơn của tôi còn nợ bao nhiêu", "SinhVien");
            string response = ResponseOf(root);

            Assert.Contains("Kết quả tra cứu Hóa đơn", response);
            Assert.Contains("Nội quy liên quan", response);
            Assert.Contains(FactGioDongCua, response);
        }

        [Fact]
        public async Task AdminBaoCaoSuCo_KhongBiHieuThanhBaoCaoTongQuan()
        {
            var root = await AskAsync("báo cáo sự cố đang chờ xử lý", "Admin");

            Assert.DoesNotContain("BÁO CÁO TỔNG QUAN", ResponseOf(root));
        }

        [Theory]
        [InlineData("giá tiền điện nước", "Admin")]
        [InlineData("giá tiền điện nước", "SinhVien")]
        [InlineData("tiền điện bao nhiêu 1 số", "Admin")]
        [InlineData("giá 1 khối nước", "SinhVien")]
        public async Task CachHoiGiaDienNuoc_TraLoiDonGia_KhongTraCongNo(string question, string role)
        {
            var root = await AskAsync(question, role);
            string response = ResponseOf(root);

            Assert.Contains(FactGiaDien, response);
            Assert.DoesNotContain("CÔNG NỢ", response);
            Assert.DoesNotContain("Kết quả tra cứu Hóa đơn", response);
        }

        [Fact]
        public async Task SinhVienHoiWifiCoMatPhi_TraLoiTuNoiQuy_KhongTraHoaDon()
        {
            var root = await AskAsync("Wifi ktx có mất phí không", "SinhVien");
            string response = ResponseOf(root);

            Assert.Contains("Wifi", response);
            Assert.DoesNotContain("Kết quả tra cứu Hóa đơn", response);
        }

        [Fact]
        public async Task AdminHoiPhongTrongVaGioGioiNghiem_TraSoLieuKemNoiQuy()
        {
            var root = await AskAsync("Có bao nhiêu phòng trống và giờ giới nghiêm là mấy giờ?", "Admin");
            string response = ResponseOf(root);

            Assert.Contains("phòng còn giường trống", response);
            Assert.Contains(FactGioDongCua, response);
        }

        [Fact]
        public async Task AdminHoiHopDongSapHetHan_TachRiengHopDongDaQuaHan()
        {
            var root = await AskAsync("Hợp đồng sắp hết hạn", "Admin");
            string response = ResponseOf(root);

            Assert.Contains("Sắp hết hạn trong 30 ngày tới", response);
            Assert.Contains("Đã quá ngày kết thúc", response);
            Assert.DoesNotContain("còn -", response);
        }

        [Fact]
        public async Task AdminHoiSuCo_KhongTinhYeuCauPhongLaSuCo()
        {
            var root = await AskAsync("Sự cố đang chờ xử lý", "Admin");
            string response = ResponseOf(root);

            Assert.Contains("SỰ CỐ KỸ THUẬT CHƯA XỬ LÝ XONG", response);
            Assert.DoesNotContain("YC_", response);
            Assert.DoesNotContain("[Xin chuyển phòng]", response);
        }

        [Theory]
        [InlineData("Viết code Python game rắn săn mồi", "SinhVien")]
        [InlineData("Ai là hiệu trưởng trường?", "Admin")]
        public async Task CauHoiNgoaiPhamVi_KhongGuiChoLLM_TraLoiKhongCoThongTin(string question, string role)
        {
            var root = await AskAsync(question, role, ollamaOnline: true);
            string response = ResponseOf(root);

            Assert.DoesNotContain("FAKE_LLM_ANSWER", response);
            Assert.Contains("chưa tìm thấy thông tin phù hợp", response);
        }

        [Fact]
        public async Task CauHoiNoiQuy_KhiOllamaChay_VanDungCauTraLoiCuaLLM()
        {
            var root = await AskAsync("KTX mấy giờ đóng cửa?", "SinhVien", ollamaOnline: true);

            Assert.Contains("FAKE_LLM_ANSWER", ResponseOf(root));
        }

        [Fact]
        public async Task AdminHoiCongNo_VanTraSoLieuCongNo()
        {
            var root = await AskAsync("danh sách hóa đơn chưa thanh toán", "Admin");

            Assert.Contains("CÔNG NỢ", ResponseOf(root));
        }
    }
}
