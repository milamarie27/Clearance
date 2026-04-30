using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using MySql.Data.MySqlClient;
using OnlineClearanceSystem.Models;
using OnlineClearanceSystem.Data;

namespace OnlineClearanceSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly IConfiguration _config;

        public HomeController(IConfiguration config)
        {
            _config = config;
        }

        // ── GET /Home/Index ────────────────────────────────────
        public IActionResult Index() => View();

        // ── GET /Home/Login ────────────────────────────────────
        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectBasedOnRole();

            if (TempData["RegisterSuccess"] != null)
                ViewBag.SuccessMessage = TempData["RegisterSuccess"];

            return View(new LoginViewModel());
        }

        // ── POST /Home/Login ───────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT id, username, password, first_name,
                           last_name, role, is_active
                    FROM users
                    WHERE username = @u LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@u", model.Username);

                using var r = cmd.ExecuteReader();
                if (!r.Read())
                {
                    ViewBag.ErrorMessage = "Invalid username or password.";
                    return View(model);
                }

                var id        = r.GetInt32("id");
                var hash      = r.GetString("password");
                var firstName = r.GetString("first_name");
                var lastName  = r.GetString("last_name");
                var isActive  = r.GetBoolean("is_active");
                var role      = r.IsDBNull(r.GetOrdinal("role"))
                                    ? null
                                    : r.GetString("role");
                r.Close();

                // Account pending — role is 'Pending' or not yet activated
                if (!isActive || role == null || role == "Pending")
                {
                    ViewBag.ErrorMessage =
                        "Your account is pending activation. " +
                        "Please wait for the Admin to assign your role " +
                        "and activate your account.";
                    return View(model);
                }

                // Verify password
                bool valid = hash.StartsWith("$2")
                    ? BCrypt.Net.BCrypt.Verify(model.Password, hash)
                    : hash == model.Password;

                if (!valid)
                {
                    ViewBag.ErrorMessage = "Invalid username or password.";
                    return View(model);
                }

                // Sign in with cookie
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, id.ToString()),
                    new Claim(ClaimTypes.Name,  $"{firstName} {lastName}"),
                    new Claim(ClaimTypes.Role,  role),
                    new Claim("FirstName",      firstName),
                    new Claim("LastName",       lastName),
                };

                var identity  = new ClaimsIdentity(
                    claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = model.RememberMe
                    });

                return RedirectBasedOnRole();
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Connection error: " + ex.Message;
                return View(model);
            }
        }

        // ── GET /Home/Register ─────────────────────────────────
        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectBasedOnRole();

            return View(new RegisterViewModel());
        }

        // ── POST /Home/Register ────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                // Check duplicate username
                var checkUsername = new MySqlCommand(
                    "SELECT COUNT(*) FROM users WHERE username = @u", conn);
                checkUsername.Parameters.AddWithValue("@u", model.Username);
                var usernameExists =
                    Convert.ToInt32(checkUsername.ExecuteScalar()) > 0;

                if (usernameExists)
                {
                    ModelState.AddModelError(
                        nameof(model.Username),
                        "That username is already taken.");
                    return View(model);
                }

                // Check duplicate ID Number
                var checkId = new MySqlCommand(
                    "SELECT COUNT(*) FROM users WHERE id_number = @id", conn);
                checkId.Parameters.AddWithValue("@id", model.IdNumber);
                var idExists =
                    Convert.ToInt32(checkId.ExecuteScalar()) > 0;

                if (idExists)
                {
                    ModelState.AddModelError(
                        nameof(model.IdNumber),
                        "That ID Number is already registered.");
                    return View(model);
                }

                // Hash password
                var hash = BCrypt.Net.BCrypt.HashPassword(model.Password);

                // ✅ FIXED: use 'Pending' instead of NULL
                var cmd = new MySqlCommand(@"
                    INSERT INTO users
                        (username, id_number, password,
                         first_name, last_name,
                         role, is_active, created_at)
                    VALUES
                        (@u, @idnum, @p,
                         @fn, @ln,
                         'Pending', 0, NOW())", conn);

                cmd.Parameters.AddWithValue("@u",     model.Username);
                cmd.Parameters.AddWithValue("@idnum", model.IdNumber);
                cmd.Parameters.AddWithValue("@p",     hash);
                cmd.Parameters.AddWithValue("@fn",    model.FirstName.Trim());
                cmd.Parameters.AddWithValue("@ln",    model.LastName.Trim());
                cmd.ExecuteNonQuery();

                TempData["RegisterSuccess"] =
                    $"Account registered for {model.FirstName} {model.LastName}. " +
                    "Please wait for the Admin to activate your account.";

                return RedirectToAction(nameof(Login));
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Error saving account: " + ex.Message;
                return View(model);
            }
        }

        // ── POST /Home/Logout ──────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        // ── GET /Home/AccessDenied ─────────────────────────────
        public IActionResult AccessDenied() => View();

        // ── Helper ─────────────────────────────────────────────
        private IActionResult RedirectBasedOnRole()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            return role switch
            {
                "Admin"      => RedirectToAction("Dashboard", "Admin"),
                "Instructor" => RedirectToAction("Dashboard", "Instructor"),
                "Student"    => RedirectToAction("Dashboard", "Student"),
                _            => RedirectToAction(nameof(Login))
            };
        }
    }
}