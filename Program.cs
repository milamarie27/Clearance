using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// ── Services ───────────────────────────────────────────────
builder.Services.AddControllersWithViews();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath        = "/Home/Login";
        options.LogoutPath       = "/Home/Logout";
        options.AccessDeniedPath = "/Home/AccessDenied";
        options.ExpireTimeSpan   = TimeSpan.FromHours(8);
    });

// Make IConfiguration injectable (for DbHelper)
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

// ── App pipeline ───────────────────────────────────────────
var app = builder.Build();

app.UseDeveloperExceptionPage();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Redirect root "/" to login page
app.MapGet("/", context =>
{
    context.Response.Redirect("/Home/Login");
    return Task.CompletedTask;
});

app.MapControllerRoute(
    name:    "default",
    pattern: "{controller=Home}/{action=Login}/{id?}");

app.Run();