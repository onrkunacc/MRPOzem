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
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);


var keysPath = Path.Combine(builder.Environment.ContentRootPath, "DataProtection-Keys");
Directory.CreateDirectory(keysPath);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    .SetApplicationName("CRMOzem")
    .SetDefaultKeyLifetime(TimeSpan.FromDays(90));


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


builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "CRMOzem.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;

    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;

    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

//  Servisler
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
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();
builder.Services.AddScoped<IHospitalIstatistikService, HospitalIstatistikService>();
builder.Services.AddScoped<IShipmentTemplateService, ShipmentTemplateService>();
builder.Services.AddScoped<IUsageTemplateService, UsageTemplateService>();
builder.Services.AddScoped<IKacAyYeterService, KacAyYeterService>();
builder.Services.AddScoped<IKotaHareketService, KotaHareketService>();
builder.Services.AddScoped<ITcmbKurService, TcmbKurService>();
builder.Services.AddScoped<ITedarikciService, TedarikciService>();
builder.Services.AddScoped<IKotaService, KotaService>();
builder.Services.AddScoped<IKotaDashboardService, KotaDashboardService>();
builder.Services.AddScoped<IStokHesaplamaService, StokHesaplamaService>();
builder.Services.AddHttpClient<ISiparisApiClient, SiparisApiClient>();
builder.Services.AddScoped<ISiparisSyncService, SiparisSyncService>();
builder.Services.AddHttpClient<IFizikiSayimApiClient, FizikiSayimApiClient>();
builder.Services.AddScoped<IFizikiSayimSyncService, FizikiSayimSyncService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
// Kimlik doğrulama ayarları
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login"; 
    options.AccessDeniedPath = "/Account/AccessDenied"; 
});

// Şifre Gereksinimleri (IdentityOptions)
builder.Services.Configure<IdentityOptions>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 4;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
});

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.ValueCountLimit = 10000;          // Default: 1024 — form değer sayısı
    options.KeyLengthLimit = 4096;            // Default: 2048 — anahtar uzunluğu
    options.ValueLengthLimit = int.MaxValue;  // Default: 4194304 (4MB)
    options.MultipartBodyLengthLimit = long.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});
// MVC model binding koleksiyon limiti (Default: 1024)
builder.Services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options =>
{
    options.MaxModelBindingCollectionSize = 10000;
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
    await TedarikciSeeder.SeedAsync(services);   
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

    // Auth, statik, identity, swagger vb. yolları es geç
    if (path.StartsWith("/account") || path.StartsWith("/identity")
        || path.StartsWith("/css") || path.StartsWith("/js")
        || path.StartsWith("/lib") || path.StartsWith("/favicon")
        || path.StartsWith("/tinydash-master"))
    {
        await next();
        return;
    }

    // Sadece kök veya Home/Index'te rol bazlı varsayılan yönlendirme yap.
    // Diğer tüm yollarda kullanıcı serbest gezsin — yetki kontrolü [Authorize]'a bırakılır.
    bool isLandingPath = path == "/" || path == "/home" || path == "/home/index";

    if (!isLandingPath)
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
            await context.SignOutAsync();
            context.Response.Redirect("/Account/Login");
            return;
        }

        var roles = await userManager.GetRolesAsync(user);

        if (roles.Contains("Yönetici"))
        {
            context.Response.Redirect("/Admin/Admin/Index");
            return;
        }
        if (roles.Contains("Muhasebe"))
        {
            context.Response.Redirect("/Muhasebe/Cari/Index");
            return;
        }
        if (roles.Contains("IT"))
        {
            context.Response.Redirect("/IT/Inventory/Index");
            return;
        }
        if (roles.Contains("İstatistik") || roles.Contains("İstatistikYonetici")
            || roles.Contains("İstatistikGiris") || roles.Contains("DepoGiris")
            || roles.Contains("IstatistikRapor") || roles.Contains("IstatistikGoruntule"))
        {
            context.Response.Redirect("/Istatistik/Dashboard/Index");
            return;
        }
        if (roles.Contains("İhale") || roles.Contains("IhaleYonetici")
            || roles.Contains("IhaleGiris") || roles.Contains("IhaleGoruntule"))
        {
            context.Response.Redirect("/Ihale/Ihale/Index");
            return;
        }
        if (roles.Contains("Evrak"))
        {
            context.Response.Redirect("/Evrak/Document/Index");
            return;
        }
        if (roles.Contains("İnsan Kaynakları"))
        {
            context.Response.Redirect("/InsanKaynaklari/Leave/Index");
            return;
        }
        // Kullanıcı/Finans vb. → Home/Index'te kalsın (varsayılan)
    }

    await next();
});

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllerRoute(
        name: "areas",
        pattern: "{area:exists}/{controller}/{action=Index}/{id?}"
    );

    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");
});

app.MapRazorPages();
app.Run();
