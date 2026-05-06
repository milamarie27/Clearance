using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;           // ← fixes MySql error here
using OnlineClearanceSystem.Models;
using OnlineClearanceSystem.Data;

namespace OnlineClearanceSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ApiController : ControllerBase
    {

        private readonly IConfiguration _config;

        public ApiController(IConfiguration config)
        {
            _config = config;
        }

        // ── POST /api/login ──────────────────────────────────
        // Mobile sends: { username, password }
        // Returns: { success, role, userId, fullName, studentNumber }
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest req)
        {
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                var cmd = new MySqlCommand(
                    "SELECT id, email, password, first_name, last_name, role, is_active FROM users WHERE email = @u LIMIT 1",
                    conn);
                cmd.Parameters.AddWithValue("@u", req.Email);

                using var r = cmd.ExecuteReader();
                if (!r.Read())
                    return Ok(new { success = false, message = "Invalid username or password." });

                var user = new User
                {
                    Id        = r.GetInt32("id"),
                    Email     = r.GetString("email"),
                    Password  = r.GetString("password"),
                    FirstName = r.GetString("first_name"),
                    LastName  = r.GetString("last_name"),
                    // ✅ AFTER
                    Role = r.IsDBNull(r.GetOrdinal("role")) ? "Pending" : r.GetString("role"), 
                    IsActive  = r.GetBoolean("is_active")
                };
                r.Close();

                if (!user.IsActive)
                    return Ok(new { success = false, message = "Account is deactivated." });

                // Check password
                bool valid = user.Password.StartsWith("$2")
                    ? BCrypt.Net.BCrypt.Verify(req.Password, user.Password)
                    : user.Password == req.Password;

                if (!valid)
                    return Ok(new { success = false, message = "Invalid username or password." });

                // Auto-detect role
                string role = user.Role ?? "Student";
                if (role != "Admin")
                {
                    var sigCmd = new MySqlCommand(
                        "SELECT employee_id FROM signatories WHERE user_id = @uid LIMIT 1", conn);
                    sigCmd.Parameters.AddWithValue("@uid", user.Id);
                    var empId = sigCmd.ExecuteScalar()?.ToString();
                    if (!string.IsNullOrEmpty(empId))
                    {
                        role = "Instructor";
                        // Get student number if instructor also has student record (rare)
                        return Ok(new
                        {
                            success    = true,
                            role,
                            userId     = user.Id,
                            fullName   = user.FullName,
                            employeeId = empId
                        });
                    }
                }

                // Get student number if student
                string? studentNumber = null;
                if (role == "Student")
                {
                    var sCmd = new MySqlCommand(
                        "SELECT student_number FROM students WHERE user_id = @uid LIMIT 1", conn);
                    sCmd.Parameters.AddWithValue("@uid", user.Id);
                    studentNumber = sCmd.ExecuteScalar()?.ToString();
                }

                return Ok(new
                {
                    success       = true,
                    role,
                    userId        = user.Id,
                    fullName      = user.FullName,
                    studentNumber = studentNumber
                });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = "Server error: " + ex.Message });
            }
        }

        // ── GET /api/student/clearance?studentNumber=xxx ─────
        // Returns student clearance status for mobile dashboard
        [HttpGet("student/clearance")]
        public IActionResult GetStudentClearance(string studentNumber)
        {
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                // Active period
                var periodCmd = new MySqlCommand(
                    "SELECT id, academic_year, semester FROM academic_periods WHERE is_active=1 LIMIT 1", conn);
                using var pr = periodCmd.ExecuteReader();
                string period = "";
                int periodId = 0;
                if (pr.Read())
                {
                    periodId = pr.GetInt32("id");
                    period   = pr.GetString("academic_year") + " — " + pr.GetString("semester");
                }
                pr.Close();

                // Subject clearances
                var subCmd = new MySqlCommand(@"
                    SELECT sub.subject_code, sub.title,
                           CONCAT(u.first_name,' ',u.last_name) AS instructor,
                           cs.status, st.label AS status_label, cs.remarks
                    FROM clearance_subjects cs
                    JOIN subject_offerings so ON so.mis_code = cs.mis_code
                    JOIN subjects sub ON sub.subject_code = so.subject_code
                    JOIN signatories sig ON sig.employee_id = so.instructor_id
                    JOIN users u ON u.id = sig.user_id
                    LEFT JOIN status_table st ON st.id = cs.status
                    WHERE cs.student_number = @sn", conn);
                subCmd.Parameters.AddWithValue("@sn", studentNumber);

                var subjects = new List<object>();
                using var sr = subCmd.ExecuteReader();
                while (sr.Read())
                {
                    subjects.Add(new
                    {
                        code       = sr.GetString("subject_code"),
                        title      = sr.GetString("title"),
                        instructor = sr.GetString("instructor"),
                        status     = sr.IsDBNull(sr.GetOrdinal("status")) ? 1 : sr.GetInt32("status"),
                        statusLabel = sr.IsDBNull(sr.GetOrdinal("status_label")) ? "Pending" : sr.GetString("status_label"),
                        remarks    = sr.IsDBNull(sr.GetOrdinal("remarks")) ? "" : sr.GetString("remarks")
                    });
                }
                sr.Close();

                // Org clearances
                var orgCmd = new MySqlCommand(@"
                    SELECT co.org_name, co.org_signatory, co.status, st.label AS status_label
                    FROM clearance_organization co
                    LEFT JOIN status_table st ON st.id = co.status
                    WHERE co.student_number = @sn", conn);
                orgCmd.Parameters.AddWithValue("@sn", studentNumber);

                var orgs = new List<object>();
                using var or2 = orgCmd.ExecuteReader();
                while (or2.Read())
                {
                    orgs.Add(new
                    {
                        orgName    = or2.IsDBNull(or2.GetOrdinal("org_name")) ? "" : or2.GetString("org_name"),
                        signatory  = or2.IsDBNull(or2.GetOrdinal("org_signatory")) ? "" : or2.GetString("org_signatory"),
                        status     = or2.IsDBNull(or2.GetOrdinal("status")) ? 1 : or2.GetInt32("status"),
                        statusLabel = or2.IsDBNull(or2.GetOrdinal("status_label")) ? "Pending" : or2.GetString("status_label")
                    });
                }
                or2.Close();

                // Announcements
                var annCmd = new MySqlCommand(
                    "SELECT title, content, type, created_at FROM announcements ORDER BY created_at DESC LIMIT 5", conn);
                var announcements = new List<object>();
                using var ar = annCmd.ExecuteReader();
                while (ar.Read())
                {
                    announcements.Add(new
                    {
                        title   = ar.GetString("title"),
                        content = ar.GetString("content"),
                        type    = ar.GetString("type"),
                        date    = ar.GetDateTime("created_at").ToString("MMM d, yyyy")
                    });
                }

                return Ok(new { period, subjects, orgs, announcements });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ── GET /api/instructor/subjects?employeeId=xxx ──────
        [HttpGet("instructor/subjects")]
        public IActionResult GetInstructorSubjects(string employeeId)
        {
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT so.mis_code, sub.title,
                           COUNT(cs.id) AS total,
                           SUM(CASE WHEN cs.status=2 THEN 1 ELSE 0 END) AS cleared
                    FROM subject_offerings so
                    JOIN subjects sub ON sub.subject_code = so.subject_code
                    LEFT JOIN clearance_subjects cs ON cs.mis_code = so.mis_code
                    WHERE so.instructor_id = @eid
                    GROUP BY so.mis_code, sub.title", conn);
                cmd.Parameters.AddWithValue("@eid", employeeId);

                var subjects = new List<object>();
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    subjects.Add(new
                    {
                        misCode = r.GetString("mis_code"),
                        title   = r.GetString("title"),
                        total   = r.GetInt32("total"),
                        cleared = r.IsDBNull(r.GetOrdinal("cleared")) ? 0 : r.GetInt32("cleared")
                    });
                }

                return Ok(new { subjects });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ── GET /api/instructor/students?misCode=xxx ─────────
        [HttpGet("instructor/students")]
        public IActionResult GetStudentsForSubject(string misCode)
        {
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT cs.id, cs.student_number, cs.status, cs.remarks,
                           CONCAT(u.first_name,' ',u.last_name) AS student_name,
                           st.label AS status_label
                    FROM clearance_subjects cs
                    JOIN students s ON s.student_number = cs.student_number
                    JOIN users u ON u.id = s.user_id
                    LEFT JOIN status_table st ON st.id = cs.status
                    WHERE cs.mis_code = @mc
                    ORDER BY student_name", conn);
                cmd.Parameters.AddWithValue("@mc", misCode);

                var students = new List<object>();
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    students.Add(new
                    {
                        id            = r.GetInt32("id"),
                        studentNumber = r.GetString("student_number"),
                        name          = r.GetString("student_name"),
                        status        = r.IsDBNull(r.GetOrdinal("status")) ? 1 : r.GetInt32("status"),
                        statusLabel   = r.IsDBNull(r.GetOrdinal("status_label")) ? "Pending" : r.GetString("status_label"),
                        remarks       = r.IsDBNull(r.GetOrdinal("remarks")) ? "" : r.GetString("remarks")
                    });
                }

                return Ok(new { students });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ── POST /api/instructor/update ───────────────────────
        // Body: { clearanceId, status, remarks }
        [HttpPost("instructor/update")]
        public IActionResult UpdateClearance([FromBody] UpdateRequest req)
        {
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    UPDATE clearance_subjects
                    SET status = @status,
                        remarks = @remarks,
                        signed_at = CASE WHEN @status = 2 THEN NOW() ELSE NULL END
                    WHERE id = @id", conn);
                cmd.Parameters.AddWithValue("@status", req.Status);
                cmd.Parameters.AddWithValue("@remarks", (object?)req.Remarks ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@id", req.ClearanceId);
                cmd.ExecuteNonQuery();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ── GET /api/admin/overview ───────────────────────────
        [HttpGet("admin/overview")]
        public IActionResult GetAdminOverview()
        {
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT s.student_number,
                           CONCAT(u.first_name,' ',u.last_name) AS full_name,
                           s.status AS student_status,
                           COUNT(cs.id) AS total,
                           SUM(CASE WHEN cs.status=2 THEN 1 ELSE 0 END) AS cleared
                    FROM students s
                    JOIN users u ON u.id = s.user_id
                    LEFT JOIN clearance_subjects cs ON cs.student_number = s.student_number
                    GROUP BY s.student_number, full_name, student_status
                    ORDER BY full_name", conn);

                var students = new List<object>();
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    int total   = r.GetInt32("total");
                    int cleared = r.IsDBNull(r.GetOrdinal("cleared")) ? 0 : r.GetInt32("cleared");
                    students.Add(new
                    {
                        studentNumber = r.GetString("student_number"),
                        fullName      = r.GetString("full_name"),
                        studentStatus = r.GetString("student_status"),
                        total,
                        cleared,
                        isFullyCleared = total > 0 && cleared == total
                    });
                }

                return Ok(new { students });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ── GET /api/admin/announcements ──────────────────────
        [HttpGet("admin/announcements")]
        public IActionResult GetAnnouncements()
        {
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand(
                    "SELECT id, title, content, type, created_at FROM announcements ORDER BY created_at DESC", conn);
                var list = new List<object>();
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new
                    {
                        id      = r.GetInt32("id"),
                        title   = r.GetString("title"),
                        content = r.GetString("content"),
                        type    = r.GetString("type"),
                        date    = r.GetDateTime("created_at").ToString("MMM d, yyyy")
                    });
                }
                return Ok(new { announcements = list });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ── POST /api/admin/announcement ──────────────────────
        [HttpPost("admin/announcement")]
        public IActionResult AddAnnouncement([FromBody] AnnouncementRequest req)
        {
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand(
                    "INSERT INTO announcements (title, content, type) VALUES (@t, @c, @ty)", conn);
                cmd.Parameters.AddWithValue("@t", req.Title);
                cmd.Parameters.AddWithValue("@c", req.Content);
                cmd.Parameters.AddWithValue("@ty", req.Type);
                cmd.ExecuteNonQuery();
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        

        // ── DELETE /api/admin/announcement/{id} ───────────────
        [HttpDelete("admin/announcement/{id}")]
        public IActionResult DeleteAnnouncement(int id)
        {
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand("DELETE FROM announcements WHERE id = @id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }

    // ── Request body models ───────────────────────────────────
    public class LoginRequest
    {
        public string Email    { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class UpdateRequest
    {
        public int ClearanceId { get; set; }
        public int Status      { get; set; }
        public string? Remarks { get; set; }
    }

    public class AnnouncementRequest
    {
        public string Title   { get; set; } = "";
        public string Content { get; set; } = "";
        public string Type    { get; set; } = "General";
    }
}