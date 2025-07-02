using CRMOzem.Business.Interfaces;
using CRMOzem.Business.Services;
using CRMOzem.Core.Models;
using CRMOzem.DataAccess.Context;
using CRMOzem.DataAccess.Seeders;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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
    var path = context.Request.Path.Value.ToLower();

    
    if (path == "/account/logout")
    {
        await next();
        return;
    }

    if (context.User.Identity.IsAuthenticated)
    {
        var userManager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.GetUserAsync(context.User);
        var roles = await userManager.GetRolesAsync(user);

        if (roles.Contains("Admin") && !path.StartsWith("/admin"))
        {
            context.Response.Redirect("/Admin/Admin/Index");
            return;
        }
        else if (roles.Contains("Muhasebe") && !path.StartsWith("/muhasebe") && !path.StartsWith("/admin") && !path.StartsWith("/evrak"))
        {
            context.Response.Redirect("/Muhasebe/Cari/Index");
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
