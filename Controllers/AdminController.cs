using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using OnlineClearanceSystem.Models;
using OnlineClearanceSystem.Data;
using System.Security.Claims;
using System.Text.Json;

namespace OnlineClearanceSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IConfiguration _config;

        public AdminController(IConfiguration config)
        {
            _config = config;
        }

        // ══════════════════════════════════════════════════
        // VIEWS
        // ══════════════════════════════════════════════════

        public IActionResult Dashboard()
        {
            ViewData["AdminName"] = $"{User.FindFirst("FirstName")?.Value} {User.FindFirst("LastName")?.Value}".Trim();
            return View();
        }

        public IActionResult Announcement() => View();

        public IActionResult Student()
        {
            ViewData["SubView"] = "all";
            return View();
        }

        public IActionResult StudentCleared()
        {
            ViewData["SubView"] = "cleared";
            return View("Student");
        }

        public IActionResult StudentIncomplete()
        {
            ViewData["SubView"] = "incomplete";
            return View("Student");
        }

        public IActionResult StudentAssign()
        {
            ViewData["SubView"] = "assign";
            return View("Student");
        }

        public IActionResult Instructor() => View();
        public IActionResult InstructorDetail() => View();
        public IActionResult Subjects() => View();
        public IActionResult Academic() => View();
        public IActionResult Staff() => View();

        // ══════════════════════════════════════════════════
        // API — DASHBOARD
        // ══════════════════════════════════════════════════

        [HttpGet("/api/admin/stats")]
        public IActionResult Stats()
        {
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                return Ok(new
                {
                    students    = GetCount(conn, "SELECT COUNT(*) FROM users WHERE role='Student' AND is_active=1"),
                    instructors = GetCount(conn, "SELECT COUNT(*) FROM users WHERE role='Instructor' AND is_active=1"),
                    staff       = GetCount(conn, "SELECT COUNT(*) FROM users WHERE role IN ('Staff','Admin') AND is_active=1"),
                    signatories = GetCount(conn, "SELECT COUNT(*) FROM signatories")
                });
            }
            catch { return Ok(new { students = 0, instructors = 0, staff = 0, signatories = 0 }); }
        }

        [HttpGet("/api/admin/active-period")]
        public IActionResult ActivePeriod()
        {
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand(
                    "SELECT id, academic_year, semester FROM academic_periods WHERE is_active=1 LIMIT 1", conn);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                    return Ok(new
                    {
                        id  = r.GetInt32("id"),
                        ay  = r.GetString("academic_year"),
                        sem = r.GetString("semester")
                    });
            }
            catch { }
            return Ok(new { id = 0, ay = (string?)null, sem = (string?)null });
        }

        [HttpGet("/api/admin/pending-users")]
        public IActionResult PendingUsers()
        {
            var items = new List<object>();
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand(@"
                    SELECT id,
                           CONCAT(first_name,' ',last_name) AS name,
                           COALESCE(id_number,'—') AS id_number
                    FROM users
                    WHERE role='Pending' OR is_active=0
                    ORDER BY created_at DESC", conn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    items.Add(new
                    {
                        id       = r.GetInt32("id"),
                        name     = r.GetString("name"),
                        idNumber = r.GetString("id_number")
                    });
            }
            catch { }
            return Ok(items);
        }

        [HttpPost("/api/admin/approve-user")]
        public IActionResult ApproveUser([FromBody] JsonElement body)
        {
            try
            {
                var id   = body.GetProperty("id").GetInt32();
                var role = body.GetProperty("role").GetString() ?? "Student";
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand(
                    "UPDATE users SET role=@r, is_active=1 WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@r",  role);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return Ok(new { success = false, error = ex.Message }); }
        }

        [HttpPost("/api/admin/decline-user")]
        public IActionResult DeclineUser([FromBody] JsonElement body)
        {
            try
            {
                var id = body.GetProperty("id").GetInt32();
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand(
                    "DELETE FROM users WHERE id=@id AND is_active=0", conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return Ok(new { success = false, error = ex.Message }); }
        }

        // ══════════════════════════════════════════════════
        // API — ANNOUNCEMENTS
        // ══════════════════════════════════════════════════

        [HttpGet("/api/admin/announcements")]
        public IActionResult GetAnnouncements()
        {
            var items = new List<object>();
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand(@"
                    SELECT id, title, content, type, created_at
                    FROM announcements
                    ORDER BY created_at DESC", conn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    items.Add(new
                    {
                        id     = r.GetInt32("id"),
                        title  = r.GetString("title"),
                        body   = r.GetString("content"),
                        type   = r.IsDBNull(r.GetOrdinal("type")) ? "General" : r.GetString("type"),
                        date   = r.GetDateTime("created_at").ToString("MMMM d, yyyy"),
                        pinned = false
                    });
            }
            catch { }
            return Ok(items);
        }

        [HttpPost("/api/admin/announcements")]
        public IActionResult CreateAnnouncement([FromBody] JsonElement body)
        {
            try
            {
                var title   = body.GetProperty("title").GetString() ?? "";
                var content = body.GetProperty("body").GetString() ?? "";
                var type    = body.GetProperty("type").GetString() ?? "General";
                var userId  = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand(@"
                    INSERT INTO announcements (title, content, type, author_id)
                    VALUES (@t, @c, @tp, @a);
                    SELECT LAST_INSERT_ID();", conn);
                cmd.Parameters.AddWithValue("@t",  title);
                cmd.Parameters.AddWithValue("@c",  content);
                cmd.Parameters.AddWithValue("@tp", type);
                cmd.Parameters.AddWithValue("@a",  userId);
                var newId = Convert.ToInt32(cmd.ExecuteScalar());
                return Ok(new { success = true, id = newId });
            }
            catch (Exception ex) { return Ok(new { success = false, error = ex.Message }); }
        }

        [HttpPut("/api/admin/announcements/{id}")]
        public IActionResult UpdateAnnouncement(int id, [FromBody] JsonElement body)
        {
            try
            {
                var title   = body.GetProperty("title").GetString() ?? "";
                var content = body.GetProperty("body").GetString() ?? "";
                var type    = body.GetProperty("type").GetString() ?? "General";
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand(
                    "UPDATE announcements SET title=@t, content=@c, type=@tp WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@t",  title);
                cmd.Parameters.AddWithValue("@c",  content);
                cmd.Parameters.AddWithValue("@tp", type);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return Ok(new { success = false, error = ex.Message }); }
        }

        [HttpDelete("/api/admin/announcements/{id}")]
        public IActionResult DeleteAnnouncement(int id)
        {
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand("DELETE FROM announcements WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return Ok(new { success = false, error = ex.Message }); }
        }

        // ══════════════════════════════════════════════════
        // API — ACADEMIC PERIODS
        // ══════════════════════════════════════════════════

        [HttpGet("/api/admin/academic")]
        public IActionResult GetAcademic()
        {
            var items = new List<object>();
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand(
                    "SELECT id, academic_year, semester, is_active FROM academic_periods ORDER BY id DESC", conn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    items.Add(new
                    {
                        id     = r.GetInt32("id"),
                        ay     = r.GetString("academic_year"),
                        sem    = r.GetString("semester"),
                        start  = "",
                        end    = "",
                        status = r.GetBoolean("is_active") ? "Active" : "Completed"
                    });
            }
            catch { }
            return Ok(items);
        }

        [HttpPost("/api/admin/academic")]
        public IActionResult CreateAcademic([FromBody] JsonElement body)
        {
            try
            {
                var ay     = body.GetProperty("ay").GetString() ?? "";
                var sem    = body.GetProperty("sem").GetString() ?? "";
                var status = body.GetProperty("status").GetString() ?? "Completed";
                var active = status == "Active" ? 1 : 0;

                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                if (active == 1)
                {
                    var deact = new MySqlCommand(
                        "UPDATE academic_periods SET is_active=0", conn);
                    deact.ExecuteNonQuery();
                }

                var cmd = new MySqlCommand(@"
                    INSERT INTO academic_periods (academic_year, semester, is_active)
                    VALUES (@ay, @sem, @act);
                    SELECT LAST_INSERT_ID();", conn);
                cmd.Parameters.AddWithValue("@ay",  ay);
                cmd.Parameters.AddWithValue("@sem", sem);
                cmd.Parameters.AddWithValue("@act", active);
                var newId = Convert.ToInt32(cmd.ExecuteScalar());
                return Ok(new { success = true, id = newId });
            }
            catch (Exception ex) { return Ok(new { success = false, error = ex.Message }); }
        }

        [HttpPut("/api/admin/academic/{id}")]
        public IActionResult UpdateAcademic(int id, [FromBody] JsonElement body)
        {
            try
            {
                var ay     = body.GetProperty("ay").GetString() ?? "";
                var sem    = body.GetProperty("sem").GetString() ?? "";
                var status = body.GetProperty("status").GetString() ?? "Completed";
                var active = status == "Active" ? 1 : 0;

                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                if (active == 1)
                {
                    var deact = new MySqlCommand(
                        "UPDATE academic_periods SET is_active=0 WHERE id != @id", conn);
                    deact.Parameters.AddWithValue("@id", id);
                    deact.ExecuteNonQuery();
                }

                var cmd = new MySqlCommand(@"
                    UPDATE academic_periods
                    SET academic_year=@ay, semester=@sem, is_active=@act
                    WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@ay",  ay);
                cmd.Parameters.AddWithValue("@sem", sem);
                cmd.Parameters.AddWithValue("@act", active);
                cmd.Parameters.AddWithValue("@id",  id);
                cmd.ExecuteNonQuery();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return Ok(new { success = false, error = ex.Message }); }
        }

        [HttpDelete("/api/admin/academic/{id}")]
        public IActionResult DeleteAcademic(int id)
        {
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand(
                    "DELETE FROM academic_periods WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return Ok(new { success = false, error = ex.Message }); }
        }

        // ══════════════════════════════════════════════════
        // API — SUBJECTS
        // ══════════════════════════════════════════════════

        [HttpGet("/api/admin/subjects")]
public IActionResult GetSubjects()
{
    var items = new List<object>();
    try
    {
        using var conn = DbHelper.GetConnection(_config);
        conn.Open();
        var cmd = new MySqlCommand(
            "SELECT id, mis_code, subject_code, title, lec_units, lab_units FROM subjects ORDER BY subject_code", conn);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            items.Add(new
            {
                id   = r.GetInt32("id"),
                mis  = r.IsDBNull(r.GetOrdinal("mis_code"))   ? "" : r.GetString("mis_code"),
                code = r.IsDBNull(r.GetOrdinal("subject_code"))? "" : r.GetString("subject_code"),
                desc = r.IsDBNull(r.GetOrdinal("title"))       ? "" : r.GetString("title"),
                lec  = r.IsDBNull(r.GetOrdinal("lec_units"))   ? 0  : r.GetInt32("lec_units"),
                lab  = r.IsDBNull(r.GetOrdinal("lab_units"))   ? 0  : r.GetInt32("lab_units")
            });
    }
    catch (Exception ex) { return Ok(new List<object>()); /* add: Console.WriteLine(ex.Message) to debug */ }
    return Ok(items);
}

        [HttpPost("/api/admin/subjects")]
public IActionResult CreateSubject([FromBody] JsonElement body)
{
    try
    {
        var mis  = body.GetProperty("mis").GetString()  ?? "";
        var code = body.GetProperty("code").GetString() ?? "";
        var desc = body.GetProperty("desc").GetString() ?? "";
        var lec  = body.GetProperty("lec").GetInt32();
        var lab  = body.GetProperty("lab").GetInt32();
        using var conn = DbHelper.GetConnection(_config);
        conn.Open();
        var cmd = new MySqlCommand(@"
            INSERT INTO subjects (mis_code, subject_code, title, lec_units, lab_units)
            VALUES (@mis, @code, @desc, @lec, @lab);
            SELECT LAST_INSERT_ID();", conn);
        cmd.Parameters.AddWithValue("@mis",  mis);
        cmd.Parameters.AddWithValue("@code", code);
        cmd.Parameters.AddWithValue("@desc", desc);
        cmd.Parameters.AddWithValue("@lec",  lec);
        cmd.Parameters.AddWithValue("@lab",  lab);
        var newId = Convert.ToInt32(cmd.ExecuteScalar());
        return Ok(new { success = true, id = newId });
    }
    catch (Exception ex) { return Ok(new { success = false, error = ex.Message }); }
}

[HttpPut("/api/admin/subjects/{id}")]
public IActionResult UpdateSubject(int id, [FromBody] JsonElement body)
{
    try
    {
        var mis  = body.GetProperty("mis").GetString()  ?? "";
        var code = body.GetProperty("code").GetString() ?? "";
        var desc = body.GetProperty("desc").GetString() ?? "";
        var lec  = body.GetProperty("lec").GetInt32();
        var lab  = body.GetProperty("lab").GetInt32();

        using var conn = DbHelper.GetConnection(_config);
        conn.Open();
        var cmd = new MySqlCommand(@"
            UPDATE subjects
            SET mis_code=@mis, subject_code=@code, title=@desc,
                lec_units=@lec, lab_units=@lab
            WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@mis",  mis);
        cmd.Parameters.AddWithValue("@code", code);
        cmd.Parameters.AddWithValue("@desc", desc);
        cmd.Parameters.AddWithValue("@lec",  lec);
        cmd.Parameters.AddWithValue("@lab",  lab);
        cmd.Parameters.AddWithValue("@id",   id);
        cmd.ExecuteNonQuery();
        return Ok(new { success = true });
    }
    catch (Exception ex) { return Ok(new { success = false, error = ex.Message }); }
}

        [HttpDelete("/api/admin/subjects/{id}")]
        public IActionResult DeleteSubject(int id)
        {
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand("DELETE FROM subjects WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return Ok(new { success = false, error = ex.Message }); }
        }

        // ══════════════════════════════════════════════════
        // API — SUBJECT OFFERINGS
        // ══════════════════════════════════════════════════

        [HttpGet("/api/admin/subject-offerings")]
        public IActionResult GetSubjectOfferings()
        {
            var items = new List<object>();
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand(@"
    SELECT so.id, so.mis_code,
           s.subject_code, s.title,
           s.lec_units, s.lab_units,
           CONCAT(u.first_name,' ',u.last_name) AS instructor,
           so.is_active
    FROM subject_offerings so
    JOIN subjects s ON s.subject_code = so.subject_code
    JOIN signatories sig ON sig.employee_id = so.instructor_id
    JOIN users u ON u.id = sig.user_id
    JOIN academic_periods ap ON ap.id = so.period_id
    ORDER BY so.id DESC", conn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    items.Add(new
                    {
                        id     = r.GetInt32("id"),
                        mis    = r.GetString("mis_code"),
                        code   = r.GetString("subject_code"),
                        desc   = r.IsDBNull(r.GetOrdinal("title"))     ? "" : r.GetString("title"),
                        lec    = r.IsDBNull(r.GetOrdinal("lec_units")) ? 0  : r.GetInt32("lec_units"),
                        lab    = r.IsDBNull(r.GetOrdinal("lab_units")) ? 0  : r.GetInt32("lab_units"),
                        inst   = r.GetString("instructor"),
                        active = r.GetBoolean("is_active")
                    });
            }
            catch { }
            return Ok(items);
        }

        [HttpPost("/api/admin/subject-offerings")]
        public IActionResult CreateOffering([FromBody] JsonElement body)
        {
            try
            {
                var mis  = body.GetProperty("mis").GetString()  ?? "";
                var code = body.GetProperty("code").GetString() ?? "";
                var inst = body.GetProperty("inst").GetString() ?? "";
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var periodCmd = new MySqlCommand(
                    "SELECT id FROM academic_periods WHERE is_active=1 LIMIT 1", conn);
                var periodId = Convert.ToInt32(periodCmd.ExecuteScalar() ?? 0);
                if (periodId == 0)
                    return Ok(new { success = false, error = "No active period" });

                var cmd = new MySqlCommand(@"
                    INSERT IGNORE INTO subject_offerings
                        (mis_code, subject_code, instructor_id, period_id)
                    VALUES (@mis, @code, @inst, @pid)", conn);
                cmd.Parameters.AddWithValue("@mis",  mis);
                cmd.Parameters.AddWithValue("@code", code);
                cmd.Parameters.AddWithValue("@inst", inst);
                cmd.Parameters.AddWithValue("@pid",  periodId);
                cmd.ExecuteNonQuery();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return Ok(new { success = false, error = ex.Message }); }
        }
        [HttpPut("/api/admin/subject-offerings/{id}")]
public IActionResult UpdateOffering(int id, [FromBody] JsonElement body)
{
    try
    {
        var active = body.GetProperty("active").GetBoolean();
        using var conn = DbHelper.GetConnection(_config);
        conn.Open();
        var cmd = new MySqlCommand(
            "UPDATE subject_offerings SET is_active=@active WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@active", active ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
        return Ok(new { success = true });
    }
    catch (Exception ex) { return Ok(new { success = false, error = ex.Message }); }
}

        // ══════════════════════════════════════════════════
        // API — COURSES
        // ══════════════════════════════════════════════════

        [HttpGet("/api/admin/courses")]
        public IActionResult GetCourses()
        {
            var items = new List<object>();
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand(
                    "SELECT id, course_code, description FROM courses ORDER BY course_code", conn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    items.Add(new
                    {
                        id         = r.GetInt32("id"),
                        code       = r.GetString("course_code"),
                        name       = r.IsDBNull(r.GetOrdinal("description")) ? "" : r.GetString("description"),
                        sections   = new List<object>(),
                        irregulars = new List<object>()
                    });
            }
            catch { }
            return Ok(items);
        }

        [HttpPost("/api/admin/courses")]
        public IActionResult CreateCourse([FromBody] JsonElement body)
        {
            try
            {
                var code = body.GetProperty("code").GetString() ?? "";
                var name = body.GetProperty("name").GetString() ?? "";
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand(@"
                    INSERT INTO courses (course_code, description) VALUES (@c, @n);
                    SELECT LAST_INSERT_ID();", conn);
                cmd.Parameters.AddWithValue("@c", code);
                cmd.Parameters.AddWithValue("@n", name);
                var newId = Convert.ToInt32(cmd.ExecuteScalar());
                return Ok(new { success = true, id = newId });
            }
            catch (Exception ex) { return Ok(new { success = false, error = ex.Message }); }
        }

        [HttpPut("/api/admin/courses/{id}")]
        public IActionResult UpdateCourse(int id, [FromBody] JsonElement body)
        {
            try
            {
                var code = body.GetProperty("code").GetString() ?? "";
                var name = body.GetProperty("name").GetString() ?? "";
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand(
                    "UPDATE courses SET course_code=@c, description=@n WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@c",  code);
                cmd.Parameters.AddWithValue("@n",  name);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return Ok(new { success = false, error = ex.Message }); }
        }

        [HttpDelete("/api/admin/courses/{id}")]
        public IActionResult DeleteCourse(int id)
        {
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand("DELETE FROM courses WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return Ok(new { success = false, error = ex.Message }); }
        }

        // ══════════════════════════════════════════════════
        // API — SECTIONS
        // ══════════════════════════════════════════════════

        [HttpGet("/api/admin/sections")]
        public IActionResult GetSections(int courseId)
        {
            var items = new List<object>();
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand(
                    "SELECT id, section_name, year_level FROM sections WHERE course_id=@cid ORDER BY id", conn);
                cmd.Parameters.AddWithValue("@cid", courseId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    items.Add(new
                    {
                        id   = r.GetInt32("id"),
                        name = r.GetString("section_name"),
                        year = r.IsDBNull(r.GetOrdinal("year_level")) ? "" : r.GetString("year_level")
                    });
            }
            catch { }
            return Ok(items);
        }

        [HttpPost("/api/admin/sections")]
        public IActionResult CreateSection([FromBody] JsonElement body)
        {
            try
            {
                var courseId = body.GetProperty("courseId").GetInt32();
                var name     = body.GetProperty("name").GetString() ?? "";
                var year     = body.GetProperty("year").GetString() ?? "";
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand(@"
                    INSERT INTO sections (course_id, section_name, year_level)
                    VALUES (@cid, @n, @y);
                    SELECT LAST_INSERT_ID();", conn);
                cmd.Parameters.AddWithValue("@cid", courseId);
                cmd.Parameters.AddWithValue("@n",   name);
                cmd.Parameters.AddWithValue("@y",   year);
                var newId = Convert.ToInt32(cmd.ExecuteScalar());
                return Ok(new { success = true, id = newId });
            }
            catch (Exception ex) { return Ok(new { success = false, error = ex.Message }); }
        }

        [HttpPut("/api/admin/sections/{id}")]
        public IActionResult UpdateSection(int id, [FromBody] JsonElement body)
        {
            try
            {
                var name = body.GetProperty("name").GetString() ?? "";
                var year = body.GetProperty("year").GetString() ?? "";
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand(
                    "UPDATE sections SET section_name=@n, year_level=@y WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@n",  name);
                cmd.Parameters.AddWithValue("@y",  year);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return Ok(new { success = false, error = ex.Message }); }
        }

        [HttpDelete("/api/admin/sections/{id}")]
        public IActionResult DeleteSection(int id)
        {
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand("DELETE FROM sections WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return Ok(new { success = false, error = ex.Message }); }
        }

// ══════════════════════════════════════════════════
// API — INSTRUCTORS
// ══════════════════════════════════════════════════
public IActionResult InstructorAssign()
{
    ViewData["SubView"] = "assign";
    return View("Instructor");
}

[HttpGet("/api/admin/instructors")]
public IActionResult GetInstructors()
{
    var items = new List<object>();
    try
    {
        using var conn = DbHelper.GetConnection(_config);
        conn.Open();
        var cmd = new MySqlCommand(@"
            SELECT id,
                   COALESCE(CONCAT(first_name,' ',last_name), email) AS name,
                   id_number,
                   created_at AS joinedDate
            FROM users
            WHERE role = 'Instructor' AND is_active = 1
            ORDER BY first_name", conn);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            items.Add(new {
                id         = r.GetInt32("id"),
                name       = r.IsDBNull(r.GetOrdinal("name"))      ? "" : r.GetString("name"),
                employeeId = r.IsDBNull(r.GetOrdinal("id_number")) ? "" : r.GetString("id_number"),
                joinedDate = r.IsDBNull(r.GetOrdinal("joinedDate")) ? "" : r.GetDateTime("joinedDate").ToString("MMM d, yyyy")
            });
    }
    catch (Exception ex) { return Ok(new { error = ex.Message }); }
    return Ok(items);
}

[HttpGet("/api/admin/instructors/{id}")]
public IActionResult GetInstructorDetail(int id)
{
    try
    {
        using var conn = DbHelper.GetConnection(_config);
        conn.Open();
        var cmd = new MySqlCommand(@"
            SELECT id,
                   first_name                     AS firstName,
                   COALESCE(middle_initial, '—')  AS middleName,
                   last_name                      AS lastName,
                   COALESCE(id_number, '—')       AS employeeId,
                   COALESCE(e_signature_path, '') AS signaturePath
            FROM users
            WHERE id = @id AND role = 'Instructor'", conn);
        cmd.Parameters.AddWithValue("@id", id);

        using var r = cmd.ExecuteReader();
        if (!r.Read())
            return Ok(new { error = "Instructor not found." });

        return Ok(new {
            firstName     = r.IsDBNull(r.GetOrdinal("firstName"))     ? "—" : r.GetString("firstName"),
            middleName    = r.IsDBNull(r.GetOrdinal("middleName"))    ? "—" : r.GetString("middleName"),
            lastName      = r.IsDBNull(r.GetOrdinal("lastName"))      ? "—" : r.GetString("lastName"),
            employeeId    = r.IsDBNull(r.GetOrdinal("employeeId"))    ? "—" : r.GetString("employeeId"),
            section       = "—",
            position      = "—",
            signaturePath = r.IsDBNull(r.GetOrdinal("signaturePath")) ? ""  : r.GetString("signaturePath")
        });
    }
    catch (Exception ex) { return Ok(new { error = ex.Message }); }
}


        // ══════════════════════════════════════════════════
        // API — STUDENTS
        // ══════════════════════════════════════════════════
        [HttpGet("/api/admin/students")]
public IActionResult GetStudents()
{
    var items = new List<object>();
    try
    {
        using var conn = DbHelper.GetConnection(_config);
        conn.Open();
        var cmd = new MySqlCommand(@"
            SELECT u.id,
                   CONCAT(u.first_name,' ',u.last_name) AS name,
                   COALESCE(s.student_number,'—')        AS student_number,
                   COALESCE(c.course_code,'—')           AS course_code,
                   COALESCE(cu.year_level, 0)            AS year_level,
                   COALESCE(cu.section,'—')              AS section,
                   CASE
                       WHEN s.student_number IS NULL THEN 'Pending'
                       WHEN (SELECT COUNT(*) FROM clearance_subjects cs2
                             WHERE cs2.student_number = s.student_number) = 0
                            THEN 'Pending'
                       WHEN (SELECT COUNT(*) FROM clearance_subjects cs
                             WHERE cs.student_number = s.student_number
                             AND cs.status != 2) = 0
                            THEN 'Cleared'
                       ELSE 'Pending'
                   END AS cs
            FROM users u
            LEFT JOIN students s    ON s.user_id    = u.id
            LEFT JOIN curriculum cu ON cu.id         = s.curriculum_id
            LEFT JOIN courses c     ON c.id          = cu.course_id
            WHERE u.role = 'Student' AND u.is_active = 1
            ORDER BY u.first_name", conn);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            items.Add(new
            {
                id      = r.GetInt32("id"),
                name    = r.GetString("name"),
                idNum   = r.GetString("student_number"),
                course  = r.GetString("course_code"),
                year    = r.GetInt32("year_level"),
                section = r.GetString("section"),
                cs      = r.GetString("cs")
            });
    }
    catch (Exception ex)
    {
        return Ok(new { error = ex.Message });
    }
    return Ok(items);
}

// ══════════════════════════════════════════════════
// API — STUDENT SIGNATORIES
// ══════════════════════════════════════════════════

[HttpGet("/api/admin/student-signatories")]
public IActionResult GetStudentSignatories()
{
    var items = new List<object>();
    try
    {
        using var conn = DbHelper.GetConnection(_config);
        conn.Open();
        var cmd = new MySqlCommand(@"
            SELECT ss.id,
                   CONCAT(u.first_name,' ',u.last_name) AS name,
                   COALESCE(s.student_number,'—')       AS student_number,
                   COALESCE(c.course_code,'—')          AS course,
                   COALESCE(cu.year_level, 0)           AS year_level,
                   COALESCE(cu.section,'—')             AS section,
                   ss.position
            FROM student_signatories ss
            JOIN  users u     ON u.id      = ss.user_id
            LEFT JOIN students s    ON s.user_id  = ss.user_id
            LEFT JOIN curriculum cu ON cu.id       = s.curriculum_id
            LEFT JOIN courses c     ON c.id        = cu.course_id
            ORDER BY ss.id", conn);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            items.Add(new {
                id       = r.GetInt32("id"),
                name     = r.GetString("name"),
                idNum    = r.IsDBNull(r.GetOrdinal("student_number")) ? "—" : r.GetString("student_number"),
                course   = r.GetString("course"),
                year     = r.GetInt32("year_level"),
                section  = r.GetString("section"),
                position = r.GetString("position")
            });
    }
    catch (Exception ex)
    {
        Console.WriteLine("GetStudentSignatories error: " + ex.Message);
    }
    return Ok(items);
}

[HttpPost("/api/admin/student-signatories")]
public IActionResult CreateStudentSignatory([FromBody] JsonElement body)
{
    try
    {
        var userId   = body.GetProperty("userId").GetInt32();
        var position = body.GetProperty("position").GetString() ?? "";
        using var conn = DbHelper.GetConnection(_config);
        conn.Open();
        var cmd = new MySqlCommand(@"
            INSERT INTO student_signatories (user_id, position)
            VALUES (@uid, @pos);
            SELECT LAST_INSERT_ID();", conn);
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@pos", position);
        var newId = Convert.ToInt32(cmd.ExecuteScalar());
        return Ok(new { success = true, id = newId });
    }
    catch (Exception ex) { return Ok(new { success = false, error = ex.Message }); }
}

[HttpDelete("/api/admin/student-signatories/{id}")]
public IActionResult DeleteStudentSignatory(int id)
{
    try
    {
        using var conn = DbHelper.GetConnection(_config);
        conn.Open();
        var cmd = new MySqlCommand(
            "DELETE FROM student_signatories WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
        return Ok(new { success = true });
    }
    catch (Exception ex) { return Ok(new { success = false, error = ex.Message }); }
}

        // ══════════════════════════════════════════════════
        // API — STAFF
        // ══════════════════════════════════════════════════

        [HttpGet("/api/admin/staff")]
        public IActionResult GetStaff()
        {
            var items = new List<object>();
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand(
                    "SELECT id, email, role FROM users WHERE role IN ('Staff','Admin') AND is_active=1 ORDER BY email",
                    conn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    items.Add(new
                    {
                        id         = r.GetInt32(0),
                        name       = r.IsDBNull(1) ? "" : r.GetString(1),
                        email      = r.IsDBNull(1) ? "" : r.GetString(1),
                        employeeId = "",
                        position   = (string?)null,
                        approved   = 0,
                        pending    = 0
                    });
            }
            catch (Exception ex)
            {
                return Ok(new[] { new { id = 0, name = "ERROR: " + ex.Message,
                    email = "error", employeeId = "", position = (string?)null,
                    approved = 0, pending = 0 } });
            }
            return Ok(items);
        }

        [HttpGet("/api/admin/staff-positions")]
        public IActionResult GetStaffPositions()
        {
            var items = new List<object>();
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand(@"
                    SELECT
                        o.id,
                        CONCAT(u.first_name,' ',u.last_name) AS name,
                        sig.employee_id                       AS eid,
                        o.position_title                      AS pos,
                        SUM(CASE WHEN co.status=2 THEN 1 ELSE 0 END) AS approved,
                        SUM(CASE WHEN co.status=1 THEN 1 ELSE 0 END) AS pending
                    FROM organizations o
                    JOIN signatories sig ON sig.employee_id = o.org_signatory
                    JOIN users u ON u.id = sig.user_id
                    LEFT JOIN clearance_organization co ON co.org_signatory = o.org_signatory
                    GROUP BY o.id, u.first_name, u.last_name, sig.employee_id, o.position_title
                    ORDER BY o.id", conn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    items.Add(new
                    {
                        id       = r.GetInt32("id"),
                        name     = r.GetString("name"),
                        eid      = r.IsDBNull(r.GetOrdinal("eid")) ? "—" : r.GetString("eid"),
                        pos      = r.IsDBNull(r.GetOrdinal("pos")) ? "—" : r.GetString("pos"),
                        approved = r.IsDBNull(r.GetOrdinal("approved")) ? 0 : Convert.ToInt32(r["approved"]),
                        pending  = r.IsDBNull(r.GetOrdinal("pending"))  ? 0 : Convert.ToInt32(r["pending"])
                    });
            }
            catch { }
            return Ok(items);
        }

        [HttpPost("/api/admin/staff-positions")]
        public IActionResult CreateStaffPosition([FromBody] JsonElement body)
        {
            try
            {
                var staffUserId = body.GetProperty("staffId").GetInt32();
                var eid         = body.GetProperty("eid").GetString() ?? "";
                var pos         = body.GetProperty("pos").GetString() ?? "";

                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                var sigCmd = new MySqlCommand(
                    "SELECT employee_id FROM signatories WHERE user_id=@uid LIMIT 1", conn);
                sigCmd.Parameters.AddWithValue("@uid", staffUserId);
                var existingEid = sigCmd.ExecuteScalar()?.ToString();

                if (string.IsNullOrEmpty(existingEid))
                {
                    var insertSig = new MySqlCommand(
                        "INSERT INTO signatories (user_id, employee_id) VALUES (@uid, @eid)", conn);
                    insertSig.Parameters.AddWithValue("@uid", staffUserId);
                    insertSig.Parameters.AddWithValue("@eid", eid);
                    insertSig.ExecuteNonQuery();
                    existingEid = eid;
                }

                var cmd = new MySqlCommand(@"
                    INSERT INTO organizations (org_name, org_signatory, position_title)
                    VALUES (@n, @eid, @pos);
                    SELECT LAST_INSERT_ID();", conn);
                cmd.Parameters.AddWithValue("@n",   pos);
                cmd.Parameters.AddWithValue("@eid", existingEid);
                cmd.Parameters.AddWithValue("@pos", pos);
                var newId = Convert.ToInt32(cmd.ExecuteScalar());
                return Ok(new { success = true, id = newId });
            }
            catch (Exception ex) { return Ok(new { success = false, error = ex.Message }); }
        }

        [HttpDelete("/api/admin/staff-positions/{id}")]
        public IActionResult DeleteStaffPosition(int id)
        {
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand("DELETE FROM organizations WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return Ok(new { success = false, error = ex.Message }); }
        }

        // ══════════════════════════════════════════════════
        // API — ORG SIGNATORIES
        // ══════════════════════════════════════════════════

        [HttpPost("/api/admin/org-signatories")]
public IActionResult CreateOrgSignatory([FromBody] JsonElement body)
{
    try
    {
        var userId = body.GetProperty("userId").GetInt32();
        var eid    = body.GetProperty("eid").GetString() ?? "";
        var pos    = body.GetProperty("pos").GetString() ?? "";

        using var conn = DbHelper.GetConnection(_config);
        conn.Open();

        // Step 1 — Check if instructor already in signatories table
        var checkCmd = new MySqlCommand(
            "SELECT employee_id FROM signatories WHERE user_id = @uid LIMIT 1", conn);
        checkCmd.Parameters.AddWithValue("@uid", userId);
        var existingEid = checkCmd.ExecuteScalar()?.ToString();

        // Step 2 — If not, insert into signatories first
        if (string.IsNullOrEmpty(existingEid))
        {
            // Use their id_number as employee_id, or generate one
            var getEid = new MySqlCommand(
                "SELECT COALESCE(id_number, CONCAT('EMP-', id)) FROM users WHERE id = @uid", conn);
            getEid.Parameters.AddWithValue("@uid", userId);
            existingEid = getEid.ExecuteScalar()?.ToString() ?? eid;

            var insertSig = new MySqlCommand(
                "INSERT INTO signatories (user_id, employee_id) VALUES (@uid, @eid)", conn);
            insertSig.Parameters.AddWithValue("@uid", userId);
            insertSig.Parameters.AddWithValue("@eid", existingEid);
            insertSig.ExecuteNonQuery();
        }

        // Step 3 — Now insert into organizations using the valid employee_id
        var cmd = new MySqlCommand(@"
            INSERT INTO organizations (org_name, org_signatory, position_title)
            VALUES (@n, @eid, @pos);
            SELECT LAST_INSERT_ID();", conn);
        cmd.Parameters.AddWithValue("@n",   pos);
        cmd.Parameters.AddWithValue("@eid", existingEid);
        cmd.Parameters.AddWithValue("@pos", pos);
        var newId = Convert.ToInt32(cmd.ExecuteScalar());

        return Ok(new { success = true, id = newId });
    }
    catch (Exception ex) { return Ok(new { success = false, error = ex.Message }); }
}
[HttpGet("/api/admin/org-signatories")]
public IActionResult GetOrgSignatories()
{
    var items = new List<object>();
    try
    {
        using var conn = DbHelper.GetConnection(_config);
        conn.Open();
        var cmd = new MySqlCommand(@"
            SELECT 
                o.id,
                u.id                                    AS userId,
                CONCAT(u.first_name,' ',u.last_name)   AS name,
                COALESCE(sig.employee_id, '—')         AS eid,
                COALESCE(o.position_title, '—')        AS pos
            FROM organizations o
            JOIN signatories sig ON sig.employee_id = o.org_signatory
            JOIN users u         ON u.id            = sig.user_id
            ORDER BY o.id", conn);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            items.Add(new {
                id     = r.GetInt32("id"),
                userId = r.GetInt32("userId"),
                name   = r.GetString("name"),
                eid    = r.GetString("eid"),
                pos    = r.GetString("pos")
            });
    }
    catch (Exception ex) 
    { 
        Console.WriteLine("GetOrgSignatories error: " + ex.Message);
    }
    return Ok(items);
}

        // ══════════════════════════════════════════════════
        // FORM POSTS (non-API)
        // ══════════════════════════════════════════════════

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult ActivateUser(int id, string role)
        {
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand(
                    "UPDATE users SET role=@r, is_active=1 WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@r",  role);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
            catch { }
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult DeactivateUser(int id)
        {
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand(
                    "UPDATE users SET is_active=0 WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
            catch { }
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult DeleteUser(int id)
        {
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand(
                    "DELETE FROM users WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
            catch { }
            return RedirectToAction(nameof(Dashboard));
        }

        // ══════════════════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════════════════

        private int GetCount(MySqlConnection conn, string sql)
        {
            var cmd = new MySqlCommand(sql, conn);
            return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
        }
    }
}