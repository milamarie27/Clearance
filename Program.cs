using Microsoft.AspNetCore.Authentication.Cookies;
using MySql.Data.MySqlClient;
using OnlineClearanceSystem.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath        = "/Home/Login";
        options.LogoutPath       = "/Home/Logout";
        options.AccessDeniedPath = "/Home/Login"; // ← redirect to Login instead
        options.ExpireTimeSpan   = TimeSpan.FromHours(8);
    });

builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

var app = builder.Build();

try
{
    using var conn = DbHelper.GetConnection(app.Configuration);
    conn.Open();
    var migrations = new[]
    {
        "ALTER TABLE signatories ADD COLUMN signature_data MEDIUMTEXT NULL",
        "ALTER TABLE clearance_organization ADD COLUMN org_signatory VARCHAR(100) NOT NULL DEFAULT ''",
        @"CREATE TABLE IF NOT EXISTS student_signatories (
            id             INT          AUTO_INCREMENT PRIMARY KEY,
            user_id        INT          NOT NULL,
            position       VARCHAR(100)           DEFAULT '',
            signature_data MEDIUMTEXT             DEFAULT NULL,
            FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
        )",
        @"INSERT IGNORE INTO signatories (user_id, employee_id)
          SELECT u.id, COALESCE(NULLIF(TRIM(u.id_number),''), CONCAT('EMP-', u.id))
          FROM users u
          WHERE u.role IN ('Instructor','Staff') AND u.is_active = 1
            AND NOT EXISTS (SELECT 1 FROM signatories s WHERE s.user_id = u.id)"
    };
    foreach (var sql in migrations)
    {
        try { new MySqlCommand(sql, conn).ExecuteNonQuery(); }
        catch { }
    }
}
catch { }

app.UseDeveloperExceptionPage();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name:    "default",
    pattern: "{controller=Home}/{action=Login}/{id?}");

app.Run();