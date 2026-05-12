using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using OnlineClearanceSystem.Data;
using System.Security.Claims;

namespace OnlineClearanceSystem.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    public class NotificationController : Controller
    {
        private readonly IConfiguration _config;

        public NotificationController(IConfiguration config)
        {
            _config = config;
        }

        // GET /api/notification/list
        [HttpGet("list")]
        public IActionResult List()
        {
            var userId = int.Parse(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "";

            var notifications = new List<object>();

            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                // 1. Recent announcements (for all roles)
                var annCmd = new MySqlCommand(@"
                    SELECT id, title, type, created_at
                    FROM announcements
                    ORDER BY created_at DESC
                    LIMIT 5", conn);

                using var ar = annCmd.ExecuteReader();
                while (ar.Read())
                {
                    notifications.Add(new
                    {
                        id      = ar.GetInt32("id"),
                        title   = ar.GetString("title"),
                        type    = ar.IsDBNull(ar.GetOrdinal("type")) ? "General" : ar.GetString("type"),
                        source  = "announcement",
                        icon    = "fa-bullhorn",
                        time    = ar.GetDateTime("created_at").ToString("MMM d, h:mm tt")
                    });
                }
                ar.Close();

                // 2. Clearance status updates (for students)
                if (role == "Student")
                {
                    var snCmd = new MySqlCommand(
                        "SELECT student_number FROM students WHERE user_id = @uid LIMIT 1", conn);
                    snCmd.Parameters.AddWithValue("@uid", userId);
                    var studentNumber = snCmd.ExecuteScalar()?.ToString() ?? "";

                    if (!string.IsNullOrEmpty(studentNumber))
                    {
                        var clrCmd = new MySqlCommand(@"
                            SELECT
                                cs.id,
                                s.subject_code,
                                st.label AS status,
                                cs.signed_at
                            FROM clearance_subjects cs
                            JOIN subject_offerings so ON so.mis_code = cs.mis_code
                            JOIN subjects s ON s.subject_code = so.subject_code
                            LEFT JOIN status_table st ON st.id = cs.status
                            WHERE cs.student_number = @sn
                            AND cs.status IN (2, 3)
                            AND cs.signed_at IS NOT NULL
                            ORDER BY cs.signed_at DESC
                            LIMIT 5", conn);
                        clrCmd.Parameters.AddWithValue("@sn", studentNumber);

                        using var cr = clrCmd.ExecuteReader();
                        while (cr.Read())
                        {
                            var status = cr.GetString("status");
                            notifications.Add(new
                            {
                                id     = cr.GetInt32("id"),
                                title  = $"{cr.GetString("subject_code")} — {status}",
                                type   = status == "Cleared" ? "Approved" : "Declined",
                                source = "clearance",
                                icon   = status == "Cleared" ? "fa-check-circle" : "fa-times-circle",
                                time   = cr.IsDBNull(cr.GetOrdinal("signed_at"))
                                            ? "" : cr.GetDateTime("signed_at").ToString("MMM d, h:mm tt")
                            });
                        }
                    }
                }

                // 3. Pending requests (for instructors)
                if (role == "Instructor")
                {
                    var pendCmd = new MySqlCommand(@"
                        SELECT COUNT(*) AS cnt
                        FROM clearance_subjects cs
                        JOIN subject_offerings so ON so.mis_code = cs.mis_code
                        WHERE so.instructor_user_id = @uid
                        AND cs.status = 1", conn);
                    pendCmd.Parameters.AddWithValue("@uid", userId);
                    var pendingCount = Convert.ToInt32(pendCmd.ExecuteScalar() ?? 0);

                    if (pendingCount > 0)
                    {
                        notifications.Insert(0, new
                        {
                            id     = 0,
                            title  = $"{pendingCount} pending clearance request{(pendingCount > 1 ? "s" : "")}",
                            type   = "Pending",
                            source = "clearance",
                            icon   = "fa-clock",
                            time   = "Now"
                        });
                    }
                }
            }
            catch { }

            // Sort by time descending, take 10
            return Json(new
            {
                count = notifications.Count,
                items = notifications.Take(10)
            });
        }
    }
}