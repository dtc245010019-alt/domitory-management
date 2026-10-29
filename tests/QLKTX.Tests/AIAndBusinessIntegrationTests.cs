using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using QLKTX.Controllers;
using QLKTX.Models;
using Xunit;

namespace QLKTX.Tests
{
    /// <summary>
    /// Fake Antiforgery cho môi trường kiểm thử tích hợp WebApplicationFactory.
    /// </summary>
    public class FakeAntiforgery : IAntiforgery
    {
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) =>
            new AntiforgeryTokenSet("test_request_token", "test_cookie_token", "__RequestVerificationToken", "X-CSRF-TOKEN");

        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) =>
            new AntiforgeryTokenSet("test_request_token", "test_cookie_token", "__RequestVerificationToken", "X-CSRF-TOKEN");

        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);

        public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;

        public void SetCookieTokenAndHeader(HttpContext httpContext) { }
    }

    /// <summary>
    /// Factory cấu hình ứng dụng cho AI và Business Integration Tests.
    /// </summary>
    public class AIAndBusinessTestFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase("AIAndBusinessIntegrationTestDb");
                });

                var antiforgeryDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAntiforgery));
                if (antiforgeryDescriptor != null)
                {
                    services.Remove(antiforgeryDescriptor);
                }
                services.AddSingleton<IAntiforgery, FakeAntiforgery>();
            });

            builder.UseEnvironment("Testing");
        }
    }

    /// <summary>
    /// HẠNG MỤC 6: Bộ Integration Tests cho Controller Nghiệp vụ & AI (Hệ thống QLKTX)
    /// - Test Case 1: POST /Phong/GoiYXepPhongAI trả về HTTP 200, success: true, recommendedRooms.
    /// - Test Case 2: POST /Portal/TomTatSuCoAI trả về HTTP 200, cấu trúc phân loại sự cố hợp lệ.
    /// - Test Case 3: POST /Portal/ThanhToan khi nhập mật khẩu sai chặn giao dịch và báo lỗi về TempData/View.
    /// </summary>
    public class AIAndBusinessIntegrationTests : IClassFixture<AIAndBusinessTestFactory>
    {
        private readonly AIAndBusinessTestFactory _factory;

        public AIAndBusinessIntegrationTests(AIAndBusinessTestFactory factory)
        {
            _factory = factory;

            // Khởi tạo và seed dữ liệu cho In-Memory Database trước khi chạy kiểm thử
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Database.EnsureCreated();
            DbInitializer.Initialize(context);
        }

        [Fact]
        public async Task TestCase1_GoiYXepPhongAI_TraVeHttp200VaRecommendedRooms()
        {
            // Arrange
            var client = _factory.CreateClient();
            var payload = new
            {
                LoaiPhong = "Phòng thường",
                Toa = "Tòa chính",
                Tang = 1,
                SoGiuongCan = 1
            };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            // Act
            var response = await client.PostAsync("/Phong/GoiYXepPhongAI", content);

            // Assert: Response HTTP 200 OK
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Kết quả JSON chứa các trường success: true, recommendedRooms
            Assert.True(root.GetProperty("success").GetBoolean(), "Trường success phải bằng true.");
            Assert.True(root.TryGetProperty("recommendedRooms", out var roomsEl), "JSON phải chứa thuộc tính recommendedRooms.");
            Assert.Equal(JsonValueKind.Array, roomsEl.ValueKind);
            Assert.True(roomsEl.GetArrayLength() > 0, "Danh sách recommendedRooms phải có ít nhất 1 phòng gợi ý.");

            // Kiểm tra cấu trúc phòng gợi ý chứa Toa và Tang đã chuẩn hóa
            var firstRoom = roomsEl[0];
            Assert.True(firstRoom.TryGetProperty("maPhong", out _));
            Assert.True(firstRoom.TryGetProperty("toa", out _));
            Assert.True(firstRoom.TryGetProperty("tang", out _));
        }

        [Fact]
        public async Task TestCase2_TomTatSuCoAI_TraVeHttp200VaCauTrucPhanLoaiSuCoHopLe()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.PostAsync("/Portal/TomTatSuCoAI", null);

            // Assert: Response HTTP 200 OK
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.GetProperty("success").GetBoolean(), "Trường success phải bằng true.");
            Assert.True(root.TryGetProperty("summary", out var summaryEl), "JSON phải chứa trường summary.");

            string summary = summaryEl.GetString() ?? "";
            Assert.False(string.IsNullOrWhiteSpace(summary), "Nội dung summary không được để trống.");

            // Kiểm tra cấu trúc phân loại hợp lệ theo nhóm kỹ thuật và thứ tự ưu tiên
            Assert.Contains("Phân loại", summary);
            Assert.Contains("Điện", summary);
            Assert.True(summary.Contains("nước", StringComparison.OrdinalIgnoreCase) || summary.Contains("thoát nước", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("ưu tiên", summary, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task TestCase3_ThanhToan_MatKhauSai_ChanGiaoDichVaBaoLoiTempDataVaView()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Lấy 1 hóa đơn chưa thanh toán của DTC001
            var unpaidInvoice = await context.HoaDons
                .FirstOrDefaultAsync(h => h.MaSV == "DTC001" && h.TrangThai != "Đã thanh toán");

            if (unpaidInvoice == null)
            {
                unpaidInvoice = new HoaDon
                {
                    MaHoaDon = "HD_TEST_PAY",
                    LoaiHoaDon = "Tiền phòng",
                    TongTien = 1500000m,
                    DonGia = 1500000m,
                    NgayLap = DateTime.Now,
                    TrangThai = "Chưa thanh toán",
                    HinhThucThanhToan = "Chưa có",
                    MaSV = "DTC001",
                    MaPhong = "101"
                };
                context.HoaDons.Add(unpaidInvoice);
                await context.SaveChangesAsync();
            }

            string targetInvoiceCode = unpaidInvoice.MaHoaDon;

            // 1. Kiểm thử thông qua HTTP Pipeline bằng WebApplicationFactory (End-to-End)
            var client = _factory.CreateClient();

            // Đăng nhập tài khoản sinh viên DTC001
            var loginContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "Username", "DTC001" },
                { "Password", "123" }
            });
            var loginResponse = await client.PostAsync("/DN/Dangnhap", loginContent);
            Assert.True(loginResponse.IsSuccessStatusCode);

            // Gửi request POST /Portal/ThanhToan với mật khẩu cố tình nhập sai
            var payContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "maHoaDon", targetInvoiceCode },
                { "phuongThuc", "Chuyển khoản VNPAY" },
                { "matKhauXacNhan", "SaiMatKhau_123456" }
            });

            var payResponse = await client.PostAsync("/Portal/ThanhToan", payContent);

            // Sau khi chuyển hướng tự động, response đạt HTTP 200 OK tại View /Portal/HoaDon
            Assert.Equal(HttpStatusCode.OK, payResponse.StatusCode);

            string htmlResult = await payResponse.Content.ReadAsStringAsync();
            string decodedHtml = WebUtility.HtmlDecode(htmlResult);

            // Xác nhận giao diện hiển thị thông báo lỗi bảo mật từ TempData/View
            Assert.Contains("Xác minh thất bại", decodedHtml);
            Assert.Contains("Mật khẩu không chính xác", decodedHtml);

            // Xác nhận trạng thái hóa đơn trong CSDL KHÔNG bị gạch nợ (chặn giao dịch thành công)
            using var verifyScope = _factory.Services.CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var invoiceAfterAttempt = await verifyContext.HoaDons
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.MaHoaDon == targetInvoiceCode);

            Assert.NotNull(invoiceAfterAttempt);
            Assert.NotEqual("Đã thanh toán", invoiceAfterAttempt.TrangThai);

            // 2. Kiểm thử trực tiếp mức Controller để xác thực thông điệp trong TempData
            var envMock = new Mock<IWebHostEnvironment>();
            var clientFactoryMock = new Mock<IHttpClientFactory>();
            var loggerMock = new Mock<ILogger<PortalController>>();
            var configMock = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();

            var controller = new PortalController(verifyContext, envMock.Object, loggerMock.Object, configMock.Object, clientFactoryMock.Object);

            var httpContext = new DefaultHttpContext();
            httpContext.Session = new MockSession();
            httpContext.Session.SetString("MaSV_DangNhap", "DTC001");
            httpContext.Session.SetString("VaiTro", "SinhVien");

            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

            var actionResult = await controller.ThanhToan(targetInvoiceCode, "Chuyển khoản VNPAY", "MatKhauSaiTuyetDoi");

            // Xác nhận chuyển hướng về action HoaDon
            var redirectResult = Assert.IsType<RedirectToActionResult>(actionResult);
            Assert.Equal("HoaDon", redirectResult.ActionName);

            // Xác nhận TempData["ErrorMessage"] chứa thông báo chặn giao dịch
            Assert.True(controller.TempData.ContainsKey("ErrorMessage"));
            string errorMsg = controller.TempData["ErrorMessage"]?.ToString() ?? "";
            Assert.Contains("Xác minh thất bại", errorMsg);
            Assert.Contains("Mật khẩu không chính xác", errorMsg);
        }
    }

#nullable disable
    /// <summary>
    /// Giả lập Session cho bài kiểm thử đơn vị Controller.
    /// </summary>
    public class MockSession : ISession
    {
        private readonly Dictionary<string, byte[]> _storage = new();

        public bool IsAvailable => true;
        public string Id => Guid.NewGuid().ToString();
        public IEnumerable<string> Keys => _storage.Keys;

        public void Clear() => _storage.Clear();

        public Task CommitAsync(System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LoadAsync(System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Remove(string key) => _storage.Remove(key);

        public void Set(string key, byte[] value) => _storage[key] = value;

        public bool TryGetValue(string key, out byte[]? value)
        {
            if (_storage.TryGetValue(key, out var val))
            {
                value = val;
                return true;
            }
            value = null;
            return false;
        }
    }
#nullable enable
}
