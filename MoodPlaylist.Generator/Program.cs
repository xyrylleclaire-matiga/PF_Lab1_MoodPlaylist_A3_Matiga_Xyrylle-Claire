using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MoodPlaylist.SQLite.Data;
using MoodPlaylist.SQLite.Services;
using MoodPlaylistGenerator.Services;
// Gamitin ang using alias para tukuyin kung saang namespace nanggagaling ang services
using ServiceReferences = MoodPlaylist.SQLite.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add SQLite database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=MoodPlaylist.db"));

// Add services
builder.Services.AddScoped<IAuthService, ServiceReferences.AuthService>(); // Pinalitan
builder.Services.AddScoped<ServiceReferences.SongService>();
builder.Services.AddScoped<ServiceReferences.PlaylistService>();
builder.Services.AddScoped<ILocalMediaService, LocalMediaService>();

// Add authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");


app.Run();
