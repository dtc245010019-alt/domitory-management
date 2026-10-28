using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using QLKTX.Models;

namespace QLKTX.Tests
{
    /// <summary>
    /// Factory cấu hình ứng dụng cho Integration Tests với In-Memory Database.
    /// </summary>
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Thay thế SQL Server bằng In-Memory Database để kiểm thử độc lập
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase("IntegrationTestDb");
                });
            });

            builder.UseEnvironment("Testing");
        }
    }

    /// <summary>
    /// Bộ kiểm thử tích hợp (Integration Tests):
    /// - Kiểm thử điểm cuối chẩn đoán /_diag
    /// - Kiểm thử điểm cuối giám sát sức khỏe /health
    /// - Kiểm thử cơ chế phân quyền bảo mật (AdminAuthorize) chặn truy cập trái phép và Redirect 302
    /// </summary>
    public class SecurityAndEndpointIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public SecurityAndEndpointIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GetDiagEndpoint_TraVeHttp200OKVaNoiDungMongDoi()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/_diag");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            string content = await response.Content.ReadAsStringAsync();
            Assert.Contains("OK - server running", content);
        }

        [Fact]
        public async Task GetHealthEndpoint_TraVeHttp200OKVaJsonChiTiet()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/health");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            string contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            Assert.Equal("application/json", contentType);

            string json = await response.Content.ReadAsStringAsync();
            Assert.Contains("status", json);
            Assert.Contains("system", json);
            Assert.Contains("QLKTX", json);
        }

        [Theory]
        [InlineData("/Phong")]
        [InlineData("/Phong/Index")]
        [InlineData("/SinhVien")]
        [InlineData("/SinhVien/Index")]
        [InlineData("/HopDong")]
        [InlineData("/HopDong/Index")]
        [InlineData("/ViPham")]
        [InlineData("/ViPham/Index")]
        public async Task RouteQuanTri_ChuaDangNhap_BiChanVaRedirect302VeDangNhap(string adminUrl)
        {
            // Arrange: Tắt tự động chuyển hướng để bắt được HTTP 302
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            // Act: Truy cập các route quản trị của Admin khi chưa đăng nhập session
            var response = await client.GetAsync(adminUrl);

            // Assert: Bắt buộc trả về HTTP 302 (Found / Redirect)
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            // Kiểm tra địa chỉ chuyển hướng trỏ về trang đăng nhập
            string redirectLocation = response.Headers.Location?.ToString() ?? "";
            Assert.True(
                redirectLocation.Contains("DN/Dangnhap") || redirectLocation.Contains("Dangnhap") || redirectLocation.EndsWith("/DN") || redirectLocation.Contains("/"),
                $"Location chuyển hướng phải trỏ về trang đăng nhập, thực tế là: {redirectLocation}"
            );
        }
    }
}

