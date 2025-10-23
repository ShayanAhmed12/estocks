using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// 👇 Get connection string from appsettings.json
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// 👇 Add MySQL DbContext
builder.Services.AddDbContext<EstocksDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// 👇 Add MVC services (controllers + views)
builder.Services.AddControllersWithViews();

// 👇 Add cookie authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Users/Login"; // redirect unauthenticated users to login
        options.LogoutPath = "/Users/Logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
    });

var app = builder.Build();

// 👇 Configure middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// 👇 Enable authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// 👇 Map the default route: /{controller=Home}/{action=Index}/{id?}
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.Run();
