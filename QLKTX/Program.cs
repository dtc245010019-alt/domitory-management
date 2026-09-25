using Microsoft.EntityFrameworkCore;
using QLKTX;
using QLKTX.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Lấy chuỗi kết nối duy nhất một lần
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// 2. Đăng ký DbContext sử dụng MySQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// 3. Đăng ký các dịch vụ (Gộp lại cho gọn)
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages(); // Giữ lại nếu bạn có dùng trang Razor

// 4. Cấu hình Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddSession(); // Bật dịch vụ Session
builder.Services.AddHttpContextAccessor();

// Đăng ký HttpClient dùng chung (đúng chuẩn, tránh rò rỉ socket) cho ChatbotController
builder.Services.AddHttpClient("Ollama", client =>
{
    client.Timeout = TimeSpan.FromSeconds(20); // fail nhanh nếu Ollama không phản hồi, thay vì đợi 100s mặc định
});

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        DbInitializer.Initialize(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Đã xảy ra lỗi khi tạo dữ liệu mẫu.");
    }
}

// 5. Cấu hình Pipeline (Thứ tự rất quan trọng)
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Chỉ redirect sang HTTPS khi thực sự có cổng HTTPS được cấu hình,
// tránh trường hợp fetch() bị lỗi kết nối khi chạy dev chỉ có profile "http"
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();

app.UseSession(); // Bắt buộc nằm giữa Routing và Authorization
app.UseAuthorization();

// Điểm kiểm tra chẩn đoán
app.MapGet("/_diag", () => Results.Text("OK - server running", "text/plain"));

// 6. Cấu hình Route (Đã trỏ đúng về Controller DN - Action Dangnhap)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=DN}/{action=Dangnhap}/{id?}");

app.MapRazorPages();


app.Run();