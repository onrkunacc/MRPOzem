using CRMOzem.Business.Extensions;
using CRMOzem.Business.Interfaces;
using CRMOzem.Business.Services;
using CRMOzem.Business.Services.Inventory;
using CRMOzem.Business.Services.Storage;
using CRMOzem.Core.Interfaces.Inventory;
using CRMOzem.Core.Interfaces.Storage;
using CRMOzem.Core.Models;
using CRMOzem.DataAccess.Context;
using CRMOzem.DataAccess.Seeders;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

//  Veritabanı Bağlantıları
var connectionString = builder.Configuration.GetConnectionString("MainDb");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

//  Identity Ayarları
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

//  Servisleri Ekliyoruz
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IExpenseReportService, ExpenseReportService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IEmailSender, EmailSender>();
builder.Services.AddScoped<IAuditReportService, AuditReportService>();
builder.Services.AddScoped<ILeaveService, LeaveService>();
builder.Services.AddScoped<IStatisticsService, StatisticsService>();
builder.Services.AddScoped<IFileParserService, FileParserService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.Configure<FileStorageOptions>(builder.Configuration.GetSection("FileStorage"));
builder.Services.AddScoped<IFileStorage, LocalFileStorage>();
builder.Services.AddScoped<IAssignmentPdfService, AssignmentPdfService>();
builder.Services.AddScoped<IIstatistikService, IstatistikService>();
builder.Services.AddScoped<IKurumService, KurumService>();
builder.Services.AddScoped<ISutComparisonService, SutComparisonService>();
builder.Services.AddScoped<IHospitalTestSetService, HospitalTestSetService>();
builder.Services.AddScoped<IMonthlyVolumeService, MonthlyVolumeService>();
builder.Services.AddScoped<ISutPointService, SutPointService>();
builder.Services.AddScoped<IIhaleService, IhaleService>();
builder.Services.AddScoped<IIhaleExportService, IhaleExportService>();

// Kimlik doğrulama ayarları
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login"; 
    options.AccessDeniedPath = "/Account/AccessDenied"; 
});

// Şifre Gereksinimlerini Düzenliyoruz
builder.Services.Configure<IdentityOptions>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 4;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
});

//  MVC ve Razor Pages
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

var supportedCultures = new[] { new CultureInfo("tr-TR") };

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("tr-TR"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});
//  ROLLERİN OLUŞTURULMASI (SEED)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await RoleSeeder.SeedRoleAsync(services);
    await UserSeeder.SeedUsersAsync(services);
}

//  Hata Yönetimi
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();


app.Use(async (context, next) =>
{
    var path = (context.Request.Path.Value ?? "").ToLowerInvariant();

    // Auth sayfalarını ve statikleri yönlendirme dışında bırakın
    if (path.StartsWith("/account") || path.StartsWith("/identity") || path.StartsWith("/css") ||
        path.StartsWith("/js") || path.StartsWith("/lib") || path.StartsWith("/favicon"))
    {
        await next();
        return;
    }

    if (context.User?.Identity?.IsAuthenticated == true)
    {
        var userManager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.GetUserAsync(context.User);

        
        if (user == null)
        {
            // en temiz yaklaşım: cookie’yi temizle ve login’e gönder
            await context.SignOutAsync();
            context.Response.Redirect("/Account/Login");
            return;
        }

        var roles = await userManager.GetRolesAsync(user);

        // Rol isimleri DB’de ne ise birebir onu kullanın (Admin/Yönetici vs)
        if (roles.Contains("Yönetici") && !path.StartsWith("/admin"))
        {
            context.Response.Redirect("/Admin/Admin/Index");
            return;
        }
        else if (roles.Contains("Muhasebe") && !path.StartsWith("/muhasebe") && !path.StartsWith("/admin") && !path.StartsWith("/evrak"))
        {
            context.Response.Redirect("/Muhasebe/Cari/Index");
            return;
        }
        else if (roles.Contains("IT") && !path.StartsWith("/it"))
        {
            context.Response.Redirect("/IT/Inventory/Index");
            return;
        }
    }

    await next();
});
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllerRoute(
        name: "areas",
        pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}"
    );

    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");
});

app.MapRazorPages();
app.Run();
