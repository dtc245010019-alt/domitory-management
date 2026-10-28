using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace QLKTX.Models
{
    /// <summary>
    /// Health Check kiểm tra tính sẵn sàng của Cơ sở dữ liệu SQL Server.
    /// </summary>
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly ApplicationDbContext _dbContext;

        public DatabaseHealthCheck(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                bool canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
                if (canConnect)
                {
                    return HealthCheckResult.Healthy("Kết nối cơ sở dữ liệu SQL Server (ApplicationDbContext) hoạt động bình thường.");
                }

                return HealthCheckResult.Unhealthy("Không thể kết nối đến cơ sở dữ liệu SQL Server.");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy($"Lỗi kết nối cơ sở dữ liệu: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Health Check kiểm tra tính sẵn sàng của Dịch vụ AI Ollama (LLM).
    /// Nếu Ollama không phản hồi trong 2s, đánh dấu Degraded vì ứng dụng tự động kích hoạt Rule-based Smart Fallback.
    /// </summary>
    public class OllamaHealthCheck : IHealthCheck
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public OllamaHealthCheck(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                string configuredUrl = _configuration["Ollama:BaseUrl"] ?? "http://127.0.0.1:11434/api/chat";
                var uri = new Uri(configuredUrl);
                // Tối ưu hóa trên Windows: Ưu tiên IPv4 127.0.0.1 để tránh trễ phân giải IPv6 ::1 của localhost
                string host = (uri.Host == "localhost") ? "127.0.0.1" : uri.Host;
                string pingUrl = $"{uri.Scheme}://{host}:{uri.Port}/api/tags";

                var client = _httpClientFactory.CreateClient();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                var response = await client.GetAsync(pingUrl, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    return HealthCheckResult.Healthy("Dịch vụ Ollama AI (LLM) đang chạy và phản hồi bình thường.", new Dictionary<string, object>
                    {
                        { "endpoint", pingUrl },
                        { "statusCode", (int)response.StatusCode }
                    });
                }

                return HealthCheckResult.Degraded($"Dịch vụ Ollama phản hồi mã trạng thái {(int)response.StatusCode}. Hệ thống đang vận hành với chế độ Smart Fallback nội bộ.");
            }
            catch (Exception ex)
            {
                // Khi Ollama offline, hệ thống vẫn duy trì 100% chức năng nhờ Rule-based Smart Fallback
                return HealthCheckResult.Degraded($"Dịch vụ Ollama AI ngoại tuyến ({ex.Message}). Hệ thống tự động chuyển sang chế độ Smart Fallback nội bộ.");
            }
        }
    }

    /// <summary>
    /// Định dạng phản hồi JSON chi tiết cho endpoint /health phục vụ Observability & Monitoring.
    /// </summary>
    public static class HealthCheckResponseWriter
    {
        public static async Task WriteDetailedJsonResponse(HttpContext context, HealthReport report)
        {
            context.Response.ContentType = "application/json; charset=utf-8";

            var response = new
            {
                status = report.Status.ToString(),
                system = "Hệ thống Quản lý Ký túc xá Tích hợp AI (QLKTX)",
                timestamp = DateTime.UtcNow.ToString("o"),
                totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    durationMs = Math.Round(e.Value.Duration.TotalMilliseconds, 2),
                    data = e.Value.Data,
                    error = e.Value.Exception?.Message
                })
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }
    }
}

