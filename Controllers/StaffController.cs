using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using OnlineClearanceSystem.Models;
using OnlineClearanceSystem.Data;
using System.Security.Claims;

namespace OnlineClearanceSystem.Controllers
{
    [Authorize(Roles = "Staff")]
    public class StaffController : Controller
    {
        private readonly IConfiguration _config;

        public StaffController(IConfiguration config)
        {
            _config = config;
        }

        // ── Dashboard ─────────────────────────────────────────
        public IActionResult Dashboard()
        {
            var userId    = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var firstName = User.FindFirst("FirstName")?.Value ?? "";
            var lastName  = User.FindFirst("LastName")?.Value ?? "";

            var model = new StaffDashboardViewModel
            {
                StaffName     = $"{firstName} {lastName}".Trim(),
                Announcements = new List<AnnouncementItem>()
            };

            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                var empId = GetEmployeeId(conn, userId);

                // Active period
                var periodCmd = new MySqlCommand(
                    "SELECT CONCAT('A.Y. ', academic_year, ', ', semester) " +
                    "FROM academic_periods WHERE is_active = 1 LIMIT 1", conn);
                var period = periodCmd.ExecuteScalar()?.ToString();
                if (!string.IsNullOrEmpty(period))
                    model.ActivePeriod = period;

                if (!string.IsNullOrEmpty(empId))
                {
                    // Approved count
                    var appCmd = new MySqlCommand(@"
                        SELECT COUNT(*) FROM clearance_organization
                        WHERE org_signatory = @eid AND status = 2", conn);
                    appCmd.Parameters.AddWithValue("@eid", empId);
                    model.Approved = Convert.ToInt32(appCmd.ExecuteScalar() ?? 0);

                    // Pending count
                    var penCmd = new MySqlCommand(@"
                        SELECT COUNT(*) FROM clearance_organization
                        WHERE org_signatory = @eid AND status = 1", conn);
                    penCmd.Parameters.AddWithValue("@eid", empId);
                    model.Pending = Convert.ToInt32(penCmd.ExecuteScalar() ?? 0);
                }

                // Announcements
                LoadAnnouncements(conn, model.Announcements);
            }
            catch { }

            return View(model);
        }

        // ── Signatories ───────────────────────────────────────
        public IActionResult Signatories()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var items  = new List<SignatoryViewModel>();

            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                var empId = GetEmployeeId(conn, userId);
                if (string.IsNullOrEmpty(empId)) return View(items);

                var cmd = new MySqlCommand(@"
                    SELECT
                        co.id                                              AS Id,
                        CONCAT(u.first_name, ' ', u.last_name)            AS StudentName,
                        s.student_number                                   AS StudentId,
                        CONCAT(c.course_code, '-', cu.year_level, cu.section) AS Course,
                        COALESCE(st.label, 'Pending')                      AS Status
                    FROM clearance_organization co
                    JOIN students      s   ON s.student_number = co.student_number
                    JOIN users         u   ON u.id             = s.user_id
                    LEFT JOIN curriculum cu ON cu.id           = s.curriculum_id
                    LEFT JOIN courses   c  ON c.id             = cu.course_id
                    LEFT JOIN status_table st ON st.id         = co.status
                    WHERE co.org_signatory = @eid
                    ORDER BY co.id", conn);
                cmd.Parameters.AddWithValue("@eid", empId);

                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    items.Add(new SignatoryViewModel
                    {
                        Id          = r.GetInt32("Id"),
                        StudentName = r.GetString("StudentName"),
                        StudentId   = r.IsDBNull(r.GetOrdinal("StudentId")) ? "—" : r.GetString("StudentId"),
                        Course      = r.IsDBNull(r.GetOrdinal("Course")) ? "—" : r.GetString("Course"),
                        Status      = r.GetString("Status")
                    });
                }
            }
            catch { }

            return View(items);
        }

        // ── Approve ───────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Approve(int id)
        {
            UpdateOrgStatus(id, 2);
            TempData["SuccessMessage"] = "Student clearance approved.";
            return RedirectToAction(nameof(Signatories));
        }

        // ── Decline ───────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Decline(int id)
        {
            UpdateOrgStatus(id, 3);
            TempData["SuccessMessage"] = "Student clearance declined.";
            return RedirectToAction(nameof(Signatories));
        }

        // ── Profile GET ───────────────────────────────────────
        public IActionResult Profile()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var model  = new StaffProfileViewModel();

            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT u.first_name, u.middle_initial, u.last_name,
                           sig.employee_id, sig.signature_data
                    FROM users u
                    LEFT JOIN signatories sig ON sig.user_id = u.id
                    WHERE u.id = @uid LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@uid", userId);

                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    model.FirstName        = r.IsDBNull(r.GetOrdinal("first_name")) ? "" : r.GetString("first_name");
                    model.MiddleInitial    = r.IsDBNull(r.GetOrdinal("middle_initial")) ? "" : r.GetString("middle_initial");
                    model.LastName         = r.IsDBNull(r.GetOrdinal("last_name")) ? "" : r.GetString("last_name");
                    model.StaffId          = r.IsDBNull(r.GetOrdinal("employee_id")) ? "—" : r.GetString("employee_id");
                    model.SignatureBase64  = r.IsDBNull(r.GetOrdinal("signature_data")) ? null : r.GetString("signature_data");
                    model.Password         = "";
                }
                r.Close();

                // Positions
                var empId = GetEmployeeId(conn, userId);
                if (!string.IsNullOrEmpty(empId))
                {
                    var posCmd = new MySqlCommand(
                        "SELECT position_title FROM organizations WHERE org_signatory = @eid ORDER BY id", conn);
                    posCmd.Parameters.AddWithValue("@eid", empId);
                    using var pr = posCmd.ExecuteReader();
                    while (pr.Read())
                    {
                        if (!pr.IsDBNull(0)) model.Positions.Add(pr.GetString(0));
                    }
                }
            }
            catch { }

            return View(model);
        }

        // ── Save Signature (AJAX) ─────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult SaveSignature([FromBody] SaveSignatureDto dto)
        {
            var userId = int.Parse(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand(
                    "UPDATE signatories SET signature_data = @sd WHERE user_id = @uid", conn);
                cmd.Parameters.AddWithValue("@sd", dto.SignatureData ?? "");
                cmd.Parameters.AddWithValue("@uid", userId);
                cmd.ExecuteNonQuery();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── Profile POST ──────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult SaveStaffProfile(StaffProfileViewModel model)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                if (!string.IsNullOrWhiteSpace(model.Password))
                {
                    var hash = BCrypt.Net.BCrypt.HashPassword(model.Password);
                    var cmd = new MySqlCommand(@"
                        UPDATE users SET first_name=@fn, middle_initial=@mi,
                        last_name=@ln, password=@pw WHERE id=@id", conn);
                    cmd.Parameters.AddWithValue("@fn", model.FirstName?.Trim() ?? "");
                    cmd.Parameters.AddWithValue("@mi", model.MiddleInitial?.Trim() ?? "");
                    cmd.Parameters.AddWithValue("@ln", model.LastName?.Trim() ?? "");
                    cmd.Parameters.AddWithValue("@pw", hash);
                    cmd.Parameters.AddWithValue("@id", userId);
                    cmd.ExecuteNonQuery();
                }
                else
                {
                    var cmd = new MySqlCommand(@"
                        UPDATE users SET first_name=@fn, middle_initial=@mi,
                        last_name=@ln WHERE id=@id", conn);
                    cmd.Parameters.AddWithValue("@fn", model.FirstName?.Trim() ?? "");
                    cmd.Parameters.AddWithValue("@mi", model.MiddleInitial?.Trim() ?? "");
                    cmd.Parameters.AddWithValue("@ln", model.LastName?.Trim() ?? "");
                    cmd.Parameters.AddWithValue("@id", userId);
                    cmd.ExecuteNonQuery();
                }

                TempData["ProfileSaved"] = "Profile updated successfully!";
            }
            catch (Exception ex)
            {
                TempData["ProfileSaved"] = "Error: " + ex.Message;
            }

            return RedirectToAction(nameof(Profile));
        }

        // ══════════════════════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════════════════════

        private string GetEmployeeId(MySqlConnection conn, int userId)
        {
            var cmd = new MySqlCommand(
                "SELECT employee_id FROM signatories WHERE user_id = @uid LIMIT 1", conn);
            cmd.Parameters.AddWithValue("@uid", userId);
            return cmd.ExecuteScalar()?.ToString() ?? "";
        }

        private void UpdateOrgStatus(int id, int status)
        {
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand(
                    "UPDATE clearance_organization SET status = @s WHERE id = @id", conn);
                cmd.Parameters.AddWithValue("@s", status);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        private void LoadAnnouncements(MySqlConnection conn, List<AnnouncementItem> list)
        {
            var cmd = new MySqlCommand(@"
                SELECT title, content, type, created_at
                FROM announcements ORDER BY created_at DESC LIMIT 10", conn);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new AnnouncementItem
                {
                    Title   = r.GetString("title"),
                    Content = r.GetString("content"),
                    Type    = r.IsDBNull(r.GetOrdinal("type")) ? "General" : r.GetString("type"),
                    Date    = r.GetDateTime("created_at").ToString("MMMM d, yyyy")
                });
            }
        }
    }

}