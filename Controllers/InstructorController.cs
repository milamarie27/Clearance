using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using OnlineClearanceSystem.Models;
using OnlineClearanceSystem.Data;
using System.Security.Claims;

namespace OnlineClearanceSystem.Controllers
{
    [Authorize(Roles = "Instructor")]
    public class InstructorController : Controller
    {
        private readonly IConfiguration _config;

        public InstructorController(IConfiguration config)
        {
            _config = config;
        }

        // ── Dashboard ─────────────────────────────────────────
        public IActionResult Dashboard()
        {
            var userId = int.Parse(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var firstName = User.FindFirst("FirstName")?.Value ?? "";
            var lastName  = User.FindFirst("LastName")?.Value ?? "";

            var model = new InstructorDashboardViewModel
            {
                InstructorName  = $"{firstName} {lastName}".Trim(),
                ActivePeriod    = "—",
                Announcements   = new List<AnnouncementItem>()
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
                    // Subject count
                    var subjCmd = new MySqlCommand(@"
                        SELECT COUNT(*) FROM subject_offerings
                        WHERE instructor_id = @eid
                        AND period_id = (
                            SELECT id FROM academic_periods WHERE is_active = 1 LIMIT 1
                        )", conn);
                    subjCmd.Parameters.AddWithValue("@eid", empId);
                    model.SubjectAssigned = Convert.ToInt32(subjCmd.ExecuteScalar() ?? 0);

                    // Student stats
                    var statsCmd = new MySqlCommand(@"
                        SELECT
                            COUNT(*)                                          AS total,
                            SUM(CASE WHEN cs.status = 2 THEN 1 ELSE 0 END)  AS cleared,
                            SUM(CASE WHEN cs.status != 2 THEN 1 ELSE 0 END) AS pending
                        FROM clearance_subjects cs
                        JOIN subject_offerings so ON so.mis_code = cs.mis_code
                        WHERE so.instructor_id = @eid
                        AND so.period_id = (
                            SELECT id FROM academic_periods WHERE is_active = 1 LIMIT 1
                        )", conn);
                    statsCmd.Parameters.AddWithValue("@eid", empId);

                    using var sr = statsCmd.ExecuteReader();
                    if (sr.Read() && !sr.IsDBNull(0))
                    {
                        model.TotalStudents   = sr.GetInt32("total");
                        model.ClearedStudents = sr.IsDBNull(sr.GetOrdinal("cleared"))
                                                    ? 0 : Convert.ToInt32(sr["cleared"]);
                        model.PendingStudents = sr.IsDBNull(sr.GetOrdinal("pending"))
                                                    ? 0 : Convert.ToInt32(sr["pending"]);
                    }
                    sr.Close();
                }

                // Announcements
                LoadAnnouncements(conn, model.Announcements);
            }
            catch { }

            return View(model);
        }

        // ── Assigned Subjects ─────────────────────────────────
        public IActionResult SubjectOfferings()
        {
            var userId = int.Parse(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var items = new List<SubjectOffering>();

            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                var empId = GetEmployeeId(conn, userId);
                if (string.IsNullOrEmpty(empId)) return View(items);

                var cmd = new MySqlCommand(@"
                    SELECT
                        so.mis_code      AS MisCode,
                        s.subject_code   AS SubjectCode,
                        s.title          AS Description,
                        s.lab_units      AS LabUnit,
                        s.lec_units      AS LecUnit
                    FROM subject_offerings so
                    JOIN subjects s ON s.subject_code = so.subject_code
                    WHERE so.instructor_id = @eid
                    AND so.period_id = (
                        SELECT id FROM academic_periods WHERE is_active = 1 LIMIT 1
                    )
                    ORDER BY s.subject_code", conn);
                cmd.Parameters.AddWithValue("@eid", empId);

                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    items.Add(new SubjectOffering
                    {
                        MisCode     = r.GetString("MisCode"),
                        SubjectCode = r.GetString("SubjectCode"),
                        Description = r.GetString("Description"),
                        LabUnit     = r.IsDBNull(r.GetOrdinal("LabUnit")) ? 0 : r.GetInt32("LabUnit"),
                        LecUnit     = r.IsDBNull(r.GetOrdinal("LecUnit")) ? 0 : r.GetInt32("LecUnit")
                    });
                }
            }
            catch { }

            return View(items);
        }

        // ── Subject Clearance Requests ────────────────────────
        public IActionResult SubjectClearance()
        {
            var userId = int.Parse(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var items = new List<ClearanceRequest>();

            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                var empId = GetEmployeeId(conn, userId);
                if (string.IsNullOrEmpty(empId)) return View(items);

                var cmd = new MySqlCommand(@"
                    SELECT
                        cs.id                                               AS Id,
                        cs.mis_code                                         AS MisCode,
                        s.subject_code                                      AS SubjectCode,
                        s.title                                             AS Description,
                        CONCAT(u.first_name, ' ', u.last_name)             AS StudentName,
                        CONCAT(c.course_code, '-', cu.year_level, cu.section) AS StudentCourse
                    FROM clearance_subjects cs
                    JOIN subject_offerings so  ON so.mis_code    = cs.mis_code
                    JOIN subjects          s   ON s.subject_code = so.subject_code
                    JOIN students          st  ON st.student_number = cs.student_number
                    JOIN users             u   ON u.id           = st.user_id
                    LEFT JOIN curriculum   cu  ON cu.id          = st.curriculum_id
                    LEFT JOIN courses      c   ON c.id           = cu.course_id
                    WHERE so.instructor_id = @eid
                    AND cs.status = 1
                    AND so.period_id = (
                        SELECT id FROM academic_periods WHERE is_active = 1 LIMIT 1
                    )
                    ORDER BY cs.id", conn);
                cmd.Parameters.AddWithValue("@eid", empId);

                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    items.Add(new ClearanceRequest
                    {
                        Id            = r.GetInt32("Id"),
                        MisCode       = r.GetString("MisCode"),
                        SubjectCode   = r.GetString("SubjectCode"),
                        Description   = r.GetString("Description"),
                        StudentName   = r.GetString("StudentName"),
                        StudentCourse = r.IsDBNull(r.GetOrdinal("StudentCourse"))
                                            ? "—" : r.GetString("StudentCourse")
                    });
                }
            }
            catch { }

            return View(items);
        }

        // ── Approve Subject Clearance ─────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Approve(int id)
        {
            UpdateClearanceStatus(id, 2); // 2 = Cleared
            TempData["SuccessMessage"] = "Student clearance approved.";
            return RedirectToAction(nameof(SubjectClearance));
        }

        // ── Decline Subject Clearance ─────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Decline(int id)
        {
            UpdateClearanceStatus(id, 3); // 3 = Declined
            TempData["SuccessMessage"] = "Student clearance declined.";
            return RedirectToAction(nameof(SubjectClearance));
        }

        // ── Organization Requests ─────────────────────────────
        public IActionResult Organization()
        {
            var userId = int.Parse(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var items = new List<OrganizationRequest>();

            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                var empId = GetEmployeeId(conn, userId);
                if (string.IsNullOrEmpty(empId)) return View(items);

                var cmd = new MySqlCommand(@"
                    SELECT
                        co.id                                              AS Id,
                        o.position_title                                   AS Position,
                        CONCAT(u.first_name, ' ', u.last_name)            AS StudentName,
                        CONCAT(c.course_code, '-', cu.year_level, cu.section) AS Course,
                        COALESCE(st.label, 'Pending')                      AS Status
                    FROM clearance_organization co
                    JOIN organizations o   ON o.org_name         = co.org_name
                    JOIN students      s   ON s.student_number   = co.student_number
                    JOIN users         u   ON u.id               = s.user_id
                    LEFT JOIN curriculum cu ON cu.id             = s.curriculum_id
                    LEFT JOIN courses   c  ON c.id               = cu.course_id
                    LEFT JOIN status_table st ON st.id           = co.status
                    WHERE co.org_signatory = @eid
                    ORDER BY co.id", conn);
                cmd.Parameters.AddWithValue("@eid", empId);

                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    items.Add(new OrganizationRequest
                    {
                        Id          = r.GetInt32("Id"),
                        Position    = r.IsDBNull(r.GetOrdinal("Position")) ? "—" : r.GetString("Position"),
                        StudentName = r.GetString("StudentName"),
                        Course      = r.IsDBNull(r.GetOrdinal("Course")) ? "—" : r.GetString("Course"),
                        Status      = r.GetString("Status")
                    });
                }
            }
            catch { }

            return View(items);
        }

        // ── Approve/Decline Org ───────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult ApproveOrg(int id)
        {
            UpdateOrgStatus(id, 2);
            TempData["SuccessMessage"] = "Organization clearance approved.";
            return RedirectToAction(nameof(Organization));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult DeclineOrg(int id)
        {
            UpdateOrgStatus(id, 3);
            TempData["SuccessMessage"] = "Organization clearance declined.";
            return RedirectToAction(nameof(Organization));
        }

        // ── Signed Clearance ──────────────────────────────────
        public IActionResult SignedClearance()
        {
            var userId = int.Parse(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var filter = Request.Query["filter"].ToString().ToLower();
            if (string.IsNullOrEmpty(filter)) filter = "all";
            ViewData["Filter"] = filter;

            var items = new List<SignedClearance>();

            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                var empId = GetEmployeeId(conn, userId);
                if (string.IsNullOrEmpty(empId)) return View(items);

                var statusFilter = filter switch
                {
                    "approved" => "AND cs.status = 2",
                    "rejected" => "AND cs.status = 3",
                    _          => "AND cs.status IN (2,3)"
                };

                var cmd = new MySqlCommand($@"
                    SELECT
                        cs.mis_code                                         AS MisCode,
                        s.subject_code                                      AS SubjectCode,
                        s.title                                             AS Description,
                        CONCAT(u.first_name, ' ', u.last_name)             AS StudentName,
                        CONCAT(c.course_code, '-', cu.year_level, cu.section) AS StudentCourse,
                        CASE WHEN cs.status = 2 THEN 'Approved' ELSE 'Declined' END AS Status
                    FROM clearance_subjects cs
                    JOIN subject_offerings so  ON so.mis_code    = cs.mis_code
                    JOIN subjects          s   ON s.subject_code = so.subject_code
                    JOIN students          st  ON st.student_number = cs.student_number
                    JOIN users             u   ON u.id           = st.user_id
                    LEFT JOIN curriculum   cu  ON cu.id          = st.curriculum_id
                    LEFT JOIN courses      c   ON c.id           = cu.course_id
                    WHERE so.instructor_id = @eid
                    {statusFilter}
                    AND so.period_id = (
                        SELECT id FROM academic_periods WHERE is_active = 1 LIMIT 1
                    )
                    ORDER BY cs.id DESC", conn);
                cmd.Parameters.AddWithValue("@eid", empId);

                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    items.Add(new SignedClearance
                    {
                        MisCode       = r.GetString("MisCode"),
                        SubjectCode   = r.GetString("SubjectCode"),
                        Description   = r.GetString("Description"),
                        StudentName   = r.GetString("StudentName"),
                        StudentCourse = r.IsDBNull(r.GetOrdinal("StudentCourse"))
                                            ? "—" : r.GetString("StudentCourse"),
                        Status        = r.GetString("Status")
                    });
                }
            }
            catch { }

            return View(items);
        }

        // ── Profile GET ───────────────────────────────────────
        public IActionResult Profile()
        {
            var userId = int.Parse(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var model = new InstructorProfileViewModel();

            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT
                        u.first_name, u.middle_initial, u.last_name, u.email,
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
                    model.EmployeeId       = r.IsDBNull(r.GetOrdinal("employee_id")) ? "—" : r.GetString("employee_id");
                    model.SignatureBase64  = r.IsDBNull(r.GetOrdinal("signature_data")) ? null : r.GetString("signature_data");
                    model.Password         = "";
                }
                r.Close();

                // Get assigned positions
                var empId = GetEmployeeId(conn, userId);
                if (!string.IsNullOrEmpty(empId))
                {
                    var posCmd = new MySqlCommand(@"
                        SELECT position_title FROM organizations
                        WHERE org_signatory = @eid
                        ORDER BY id", conn);
                    posCmd.Parameters.AddWithValue("@eid", empId);

                    using var pr = posCmd.ExecuteReader();
                    while (pr.Read())
                    {
                        if (!pr.IsDBNull(0))
                            model.Positions.Add(pr.GetString(0));
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
        public IActionResult SaveInstructorProfile(InstructorProfileViewModel model)
        {
            var userId = int.Parse(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                if (!string.IsNullOrWhiteSpace(model.Password))
                {
                    var hash = BCrypt.Net.BCrypt.HashPassword(model.Password);
                    var cmd = new MySqlCommand(@"
                        UPDATE users SET
                            first_name = @fn, middle_initial = @mi,
                            last_name = @ln, password = @pw
                        WHERE id = @id", conn);
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
                        UPDATE users SET
                            first_name = @fn, middle_initial = @mi,
                            last_name = @ln
                        WHERE id = @id", conn);
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

        private void UpdateClearanceStatus(int id, int status)
        {
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand(
                    "UPDATE clearance_subjects SET status = @s, signed_at = NOW() WHERE id = @id", conn);
                cmd.Parameters.AddWithValue("@s", status);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
            catch { }
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