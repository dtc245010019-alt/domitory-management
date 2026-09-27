using Microsoft.EntityFrameworkCore;
using QLKTX;
using QLKTX.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Đăng ký DbContext kết nối SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// 2. Đăng ký MVC & Razor Pages
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// 3. Cấu hình Session & HttpContextAccessor
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.Name = ".QLKTX.Session";
});
builder.Services.AddHttpContextAccessor();

// 4. Đăng ký HttpClient cho Chatbot
builder.Services.AddHttpClient("Ollama", client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});

var app = builder.Build();

// 5. Khởi tạo dữ liệu mẫu (Seed Data)
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

// 6. Cấu hình Pipeline Middleware
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();

app.UseSession();
app.UseAuthorization();

// Endpoint kiểm tra
app.MapGet("/_diag", () => Results.Text("OK - server running", "text/plain"));

// Route mặc định (Trỏ về Controller DN - Action Dangnhap)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=DN}/{action=Dangnhap}/{id?}");

app.MapRazorPages();

app.Run();