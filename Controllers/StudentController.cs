using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using OnlineClearanceSystem.Models;
using OnlineClearanceSystem.Data;
using System.Security.Claims;

namespace OnlineClearanceSystem.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly IConfiguration _config;

        public StudentController(IConfiguration config)
        {
            _config = config;
        }

        // ── Dashboard ─────────────────────────────────────────
        public IActionResult Dashboard()
        {
            SetUserViewData();

            var userId = int.Parse(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            var model = new StudentDashboardViewModel
            {
                StudentName       = ViewData["Email"]?.ToString() ?? "Student",
                SubjectCleared    = 0,
                SubjectIncomplete = 0,
                OrgCleared        = 0,
                TotalSubjects     = 0,
                TotalOrgs         = 0,
                ActivePeriod      = "A.Y. 2025-2026, 2nd Semester",
                Announcements     = new List<AnnouncementItem>()
            };

            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                var snCmd = new MySqlCommand(
                    "SELECT student_number FROM students WHERE user_id = @uid LIMIT 1", conn);
                snCmd.Parameters.AddWithValue("@uid", userId);
                var studentNumber = snCmd.ExecuteScalar()?.ToString() ?? "";

                var periodCmd = new MySqlCommand(
                    "SELECT CONCAT('A.Y. ', academic_year, ', ', semester) " +
                    "FROM academic_periods WHERE is_active = 1 LIMIT 1", conn);
                var period = periodCmd.ExecuteScalar()?.ToString();
                if (!string.IsNullOrEmpty(period)) model.ActivePeriod = period;

                var subjCmd = new MySqlCommand(@"
                    SELECT
                        COUNT(*)                                          AS total,
                        SUM(CASE WHEN status = 2 THEN 1 ELSE 0 END)      AS cleared,
                        SUM(CASE WHEN status != 2 THEN 1 ELSE 0 END)     AS incomplete
                    FROM clearance_subjects
                    WHERE student_number = @sn", conn);
                subjCmd.Parameters.AddWithValue("@sn", studentNumber);

                using var sr = subjCmd.ExecuteReader();
                if (sr.Read() && !sr.IsDBNull(0))
                {
                    model.TotalSubjects     = sr.GetInt32("total");
                    model.SubjectCleared    = sr.IsDBNull(sr.GetOrdinal("cleared"))
                                                ? 0 : Convert.ToInt32(sr["cleared"]);
                    model.SubjectIncomplete = sr.IsDBNull(sr.GetOrdinal("incomplete"))
                                                ? 0 : Convert.ToInt32(sr["incomplete"]);
                }
                sr.Close();

                var orgCmd = new MySqlCommand(@"
                    SELECT
                        COUNT(*)                                         AS total,
                        SUM(CASE WHEN co.status = 2 THEN 1 ELSE 0 END)  AS cleared
                    FROM clearance_organization co
                    WHERE co.student_number = @sn", conn);
                orgCmd.Parameters.AddWithValue("@sn", studentNumber);

                using var or2 = orgCmd.ExecuteReader();
                if (or2.Read() && !or2.IsDBNull(0))
                {
                    model.TotalOrgs  = or2.GetInt32("total");
                    model.OrgCleared = or2.IsDBNull(or2.GetOrdinal("cleared"))
                                        ? 0 : Convert.ToInt32(or2["cleared"]);
                }
                or2.Close();

                var annCmd = new MySqlCommand(@"
                    SELECT title, content, type, created_at
                    FROM announcements
                    ORDER BY created_at DESC
                    LIMIT 10", conn);

                using var ar = annCmd.ExecuteReader();
                while (ar.Read())
                {
                    model.Announcements.Add(new AnnouncementItem
                    {
                        Title   = ar.GetString("title"),
                        Content = ar.GetString("content"),
                        Type    = ar.IsDBNull(ar.GetOrdinal("type"))
                                    ? "General" : ar.GetString("type"),
                        Date    = ar.GetDateTime("created_at").ToString("MMMM d, yyyy")
                    });
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Dashboard error: " + ex.Message;
            }

            return View(model);
        }

        // ── Subjects Offered ──────────────────────────────────
        public IActionResult SubjectsOffered()
        {
            SetUserViewData();
            var model = new SubjectOfferedViewModel();

            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                var periodCmd = new MySqlCommand(
                    "SELECT CONCAT('A.Y. ', academic_year, ', ', semester) " +
                    "FROM academic_periods WHERE is_active = 1 LIMIT 1", conn);
                var period = periodCmd.ExecuteScalar()?.ToString();
                if (!string.IsNullOrEmpty(period)) model.ActivePeriod = period;

                var cmd = new MySqlCommand(@"
                    SELECT
                        so.mis_code                                     AS Id,
                        so.mis_code                                     AS MisCode,
                        s.subject_code                                  AS SubjectCode,
                        s.title                                         AS Description,
                        COALESCE(
                            CONCAT(u.first_name, ' ', u.last_name),
                            'TBA'
                        )                                               AS InstructorName
                    FROM subject_offerings  so
                    JOIN      subjects      s   ON s.subject_code  = so.subject_code
                    LEFT JOIN signatories   sig ON sig.employee_id = so.instructor_id
                    LEFT JOIN users         u   ON u.id            = sig.user_id
                    WHERE so.is_active = 1
                    ORDER BY s.subject_code", conn);

                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    model.AvailableSubjects.Add(new SubjectItem
                    {
                        Id             = r.GetString("Id"),
                        MisCode        = r.GetString("MisCode"),
                        SubjectCode    = r.GetString("SubjectCode"),
                        Description    = r.GetString("Description"),
                        InstructorName = r.GetString("InstructorName")
                    });
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Could not load subjects: " + ex.Message;
            }

            return View(model);
        }

        // ── Confirm Subjects POST ─────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ConfirmSubjects(string selectedSubjects)
        {
            if (string.IsNullOrWhiteSpace(selectedSubjects))
                return RedirectToAction(nameof(SubjectsOffered));

            var userId = int.Parse(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                var snCmd = new MySqlCommand(
                    "SELECT student_number FROM students WHERE user_id = @uid LIMIT 1", conn);
                snCmd.Parameters.AddWithValue("@uid", userId);
                var studentNumber = snCmd.ExecuteScalar()?.ToString() ?? "";

                if (string.IsNullOrEmpty(studentNumber))
                {
                    TempData["Error"] = "Student record not found. Please contact the Admin.";
                    return RedirectToAction(nameof(SubjectsOffered));
                }

                var periodCmd = new MySqlCommand(
                    "SELECT id FROM academic_periods WHERE is_active = 1 LIMIT 1", conn);
                var periodId = Convert.ToInt32(periodCmd.ExecuteScalar() ?? 1);

                // Delete previous selections for this student + period
                var deleteCmd = new MySqlCommand(@"
                    DELETE FROM clearance_subjects
                    WHERE student_number = @sn AND period_id = @pid", conn);
                deleteCmd.Parameters.AddWithValue("@sn",  studentNumber);
                deleteCmd.Parameters.AddWithValue("@pid", periodId);
                deleteCmd.ExecuteNonQuery();

                // Insert new selections
                var codes = selectedSubjects.Split(',',
                    StringSplitOptions.RemoveEmptyEntries);

                foreach (var code in codes)
                {
                    var insertCmd = new MySqlCommand(@"
                        INSERT IGNORE INTO clearance_subjects
                            (student_number, mis_code, status, period_id)
                        VALUES (@sn, @mc, 1, @pid)", conn);
                    insertCmd.Parameters.AddWithValue("@sn",  studentNumber);
                    insertCmd.Parameters.AddWithValue("@mc",  code.Trim());
                    insertCmd.Parameters.AddWithValue("@pid", periodId);
                    insertCmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error saving subjects: " + ex.Message;
                return RedirectToAction(nameof(SubjectsOffered));
            }

            return RedirectToAction(nameof(Clearance));
        }

        // ── Clearance ─────────────────────────────────────────
        public IActionResult Clearance()
        {
            SetUserViewData();

            var userId = int.Parse(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var items = new List<StudentClearanceItem>();

            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                var snCmd = new MySqlCommand(
                    "SELECT student_number FROM students WHERE user_id = @uid LIMIT 1", conn);
                snCmd.Parameters.AddWithValue("@uid", userId);
                var studentNumber = snCmd.ExecuteScalar()?.ToString() ?? "";

                var cmd = new MySqlCommand(@"
                    SELECT
                        cs.mis_code                                     AS MisCode,
                        COALESCE(s.subject_code, cs.mis_code)           AS SubjectCode,
                        COALESCE(s.title, '—')                          AS Description,
                        COALESCE(
                            CONCAT(u.first_name, ' ', u.last_name),
                            'TBA'
                        )                                               AS InstructorName,
                        COALESCE(st.label, 'Pending')                   AS Status
                    FROM clearance_subjects cs
                    LEFT JOIN subject_offerings  so  ON so.mis_code     = cs.mis_code
                    LEFT JOIN subjects           s   ON s.subject_code  = so.subject_code
                    LEFT JOIN signatories        sig ON sig.employee_id = so.instructor_id
                    LEFT JOIN users              u   ON u.id            = sig.user_id
                    LEFT JOIN status_table       st  ON st.id           = cs.status
                    WHERE cs.student_number = @sn
                    ORDER BY cs.id", conn);
                cmd.Parameters.AddWithValue("@sn", studentNumber);

                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    items.Add(new StudentClearanceItem
                    {
                        MisCode        = r.GetString("MisCode"),
                        SubjectCode    = r.GetString("SubjectCode"),
                        Description    = r.GetString("Description"),
                        InstructorName = r.GetString("InstructorName"),
                        Status         = r.GetString("Status")
                    });
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Could not load clearance: " + ex.Message;
            }

            return View(items);
        }

        // ── Request Subject Signature (AJAX POST) ─────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RequestSubjectSignature([FromBody] RequestSubjectDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.MisCode))
                return Json(new { success = false, error = "Invalid request." });

            var userId = int.Parse(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                var snCmd = new MySqlCommand(
                    "SELECT student_number FROM students WHERE user_id = @uid LIMIT 1", conn);
                snCmd.Parameters.AddWithValue("@uid", userId);
                var studentNumber = snCmd.ExecuteScalar()?.ToString() ?? "";

                if (string.IsNullOrEmpty(studentNumber))
                    return Json(new { success = false, error = "Student record not found." });

                var periodCmd = new MySqlCommand(
                    "SELECT id FROM academic_periods WHERE is_active = 1 LIMIT 1", conn);
                var periodId = Convert.ToInt32(periodCmd.ExecuteScalar() ?? 1);

                // Upsert — never downgrade an already-Cleared (status=2) row
                var upsertCmd = new MySqlCommand(@"
                    INSERT INTO clearance_subjects
                        (student_number, mis_code, status, period_id)
                    VALUES
                        (@sn, @mis, 1, @pid)
                    ON DUPLICATE KEY UPDATE
                        status = IF(status = 2, status, 1)", conn);
                upsertCmd.Parameters.AddWithValue("@sn",  studentNumber);
                upsertCmd.Parameters.AddWithValue("@mis", dto.MisCode);
                upsertCmd.Parameters.AddWithValue("@pid", periodId);
                upsertCmd.ExecuteNonQuery();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── Organization ──────────────────────────────────────
        public IActionResult Organization()
        {
            SetUserViewData();

            var userId = int.Parse(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var items = new List<OrganizationSignatory>();

            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                var studentCmd = new MySqlCommand(@"
                    SELECT student_number, curriculum_id
                    FROM students WHERE user_id = @uid LIMIT 1", conn);
                studentCmd.Parameters.AddWithValue("@uid", userId);

                string studentNumber = "";
                int    curriculumId  = 0;

                using var sr = studentCmd.ExecuteReader();
                if (sr.Read())
                {
                    studentNumber = sr.IsDBNull(sr.GetOrdinal("student_number"))
                                    ? "" : sr.GetString("student_number");
                    curriculumId  = sr.IsDBNull(sr.GetOrdinal("curriculum_id"))
                                    ? 0  : sr.GetInt32("curriculum_id");
                }
                sr.Close();

                var cmd = new MySqlCommand(@"
                    SELECT
                        o.position_title               AS OrgRole,
                        o.org_signatory                AS PersonName,
                        COALESCE(st.label, 'None')     AS Status
                    FROM organizations o
                    LEFT JOIN clearance_organization co
                           ON co.org_name       = o.org_name
                          AND co.student_number = @sn
                    LEFT JOIN status_table st ON st.id = co.status
                    WHERE o.curriculum_id = @cid
                    ORDER BY o.id", conn);
                cmd.Parameters.AddWithValue("@sn",  studentNumber);
                cmd.Parameters.AddWithValue("@cid", curriculumId);

                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    items.Add(new OrganizationSignatory
                    {
                        OrgRole    = r.IsDBNull(r.GetOrdinal("OrgRole"))
                                        ? "—" : r.GetString("OrgRole"),
                        PersonName = r.IsDBNull(r.GetOrdinal("PersonName"))
                                        ? "—" : r.GetString("PersonName"),
                        Status     = r.GetString("Status")
                    });
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Could not load organizations: " + ex.Message;
            }

            return View(items);
        }

        // ── Profile GET ───────────────────────────────────────
        public IActionResult Profile()
        {
            SetUserViewData();

            var userId = int.Parse(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var model = new StudentProfileViewModel();

            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                // Load available courses for the dropdown
                var coursesCmd = new MySqlCommand(
                    "SELECT course_code FROM courses ORDER BY course_code", conn);
                using var cr = coursesCmd.ExecuteReader();
                while (cr.Read())
                    model.AvailableCourses.Add(cr.GetString("course_code"));
                cr.Close();

                var cmd = new MySqlCommand(@"
                    SELECT
                        u.first_name, u.middle_initial,
                        u.last_name, u.suffix_name, u.email,
                        u.id_number,
                        s.student_number,
                        c.course_code,
                        cu.year_level,
                        cu.section
                    FROM users u
                    LEFT JOIN students   s  ON s.user_id  = u.id
                    LEFT JOIN curriculum cu ON cu.id       = s.curriculum_id
                    LEFT JOIN courses    c  ON c.id        = cu.course_id
                    WHERE u.id = @uid LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@uid", userId);

                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    var studentNum = r.IsDBNull(r.GetOrdinal("student_number"))
                                        ? null : r.GetString("student_number");
                    var idNumber   = r.IsDBNull(r.GetOrdinal("id_number"))
                                        ? null : r.GetString("id_number");
                    model.StudentId     = studentNum ?? idNumber ?? "";
                    model.FirstName     = r.IsDBNull(r.GetOrdinal("first_name"))
                                            ? "" : r.GetString("first_name");
                    model.MiddleInitial = r.IsDBNull(r.GetOrdinal("middle_initial"))
                                            ? "" : r.GetString("middle_initial");
                    model.LastName      = r.IsDBNull(r.GetOrdinal("last_name"))
                                            ? "" : r.GetString("last_name");
                    model.Suffix        = r.IsDBNull(r.GetOrdinal("suffix_name"))
                                            ? "" : r.GetString("suffix_name");
                    model.Email      = r.IsDBNull(r.GetOrdinal("email"))
                                            ? "" : r.GetString("email");
                    model.Course        = r.IsDBNull(r.GetOrdinal("course_code"))
                                            ? "" : r.GetString("course_code");

                    if (!r.IsDBNull(r.GetOrdinal("year_level")))
                    {
                        model.YearLevel = r.GetInt32("year_level") switch
                        {
                            1 => "1st Year",
                            2 => "2nd Year",
                            3 => "3rd Year",
                            _ => "4th Year"
                        };
                    }

                    model.Section  = r.IsDBNull(r.GetOrdinal("section"))
                                        ? "" : r.GetString("section");
                    model.Password = "";
                }
                r.Close();

                // Load student org positions + saved signature
                var posCmd = new MySqlCommand(
                    "SELECT position, signature_data FROM student_signatories WHERE user_id = @uid ORDER BY id", conn);
                posCmd.Parameters.AddWithValue("@uid", userId);
                using var pr = posCmd.ExecuteReader();
                while (pr.Read())
                {
                    model.Positions.Add(new OrganizationSignatory
                    {
                        OrgRole    = pr.IsDBNull(pr.GetOrdinal("position")) ? "" : pr.GetString("position"),
                        PersonName = "",
                        Status     = ""
                    });
                    if (model.SignaturePath == null && !pr.IsDBNull(pr.GetOrdinal("signature_data")))
                        model.SignaturePath = pr.GetString("signature_data");
                }
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Error loading profile: " + ex.Message;
            }

            return View(model);
        }

        // ── Profile POST ──────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveProfile(StudentProfileViewModel model)
        {
            var userId = int.Parse(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                // Save user fields
                if (!string.IsNullOrWhiteSpace(model.Password))
                {
                    var hash = BCrypt.Net.BCrypt.HashPassword(model.Password);
                    var cmd  = new MySqlCommand(@"
                        UPDATE users SET
                            first_name = @fn, middle_initial = @mi,
                            last_name  = @ln, suffix_name    = @sx,
                            email   = @em, password       = @pw
                        WHERE id = @id", conn);
                    cmd.Parameters.AddWithValue("@fn", model.FirstName?.Trim()     ?? "");
                    cmd.Parameters.AddWithValue("@mi", model.MiddleInitial?.Trim() ?? "");
                    cmd.Parameters.AddWithValue("@ln", model.LastName?.Trim()      ?? "");
                    cmd.Parameters.AddWithValue("@sx", model.Suffix?.Trim()        ?? "");
                    cmd.Parameters.AddWithValue("@un", model.Email?.Trim()      ?? "");
                    cmd.Parameters.AddWithValue("@pw", hash);
                    cmd.Parameters.AddWithValue("@id", userId);
                    cmd.ExecuteNonQuery();
                }
                else
                {
                    var cmd = new MySqlCommand(@"
                        UPDATE users SET
                            first_name = @fn, middle_initial = @mi,
                            last_name  = @ln, suffix_name    = @sx,
                            email   = @em
                        WHERE id = @id", conn);
                    cmd.Parameters.AddWithValue("@fn", model.FirstName?.Trim()     ?? "");
                    cmd.Parameters.AddWithValue("@mi", model.MiddleInitial?.Trim() ?? "");
                    cmd.Parameters.AddWithValue("@ln", model.LastName?.Trim()      ?? "");
                    cmd.Parameters.AddWithValue("@sx", model.Suffix?.Trim()        ?? "");
                    cmd.Parameters.AddWithValue("@em", model.Email?.Trim()      ?? "");
                    cmd.Parameters.AddWithValue("@id", userId);
                    cmd.ExecuteNonQuery();
                }

                // Save student number + curriculum
                var studentNumber = model.StudentId?.Trim() ?? "";
                var courseCode    = model.Course?.Trim()    ?? "";
                var section       = model.Section?.Trim()   ?? "";
                var yearInt = model.YearLevel switch
                {
                    "1st Year" => 1,
                    "2nd Year" => 2,
                    "3rd Year" => 3,
                    "4th Year" => 4,
                    _          => 0
                };

                int curriculumId = 0;
                if (!string.IsNullOrEmpty(courseCode) && yearInt > 0)
                {
                    var courseCmd = new MySqlCommand(
                        "SELECT id FROM courses WHERE course_code=@c LIMIT 1", conn);
                    courseCmd.Parameters.AddWithValue("@c", courseCode);
                    var courseId = Convert.ToInt32(courseCmd.ExecuteScalar() ?? 0);

                    if (courseId > 0)
                    {
                        var findCmd = new MySqlCommand(@"
                            SELECT id FROM curriculum
                            WHERE course_id=@cid AND year_level=@yl AND section=@sec LIMIT 1", conn);
                        findCmd.Parameters.AddWithValue("@cid", courseId);
                        findCmd.Parameters.AddWithValue("@yl",  yearInt);
                        findCmd.Parameters.AddWithValue("@sec", section);
                        var existing = findCmd.ExecuteScalar();

                        if (existing != null && existing != DBNull.Value)
                        {
                            curriculumId = Convert.ToInt32(existing);
                        }
                        else
                        {
                            var newCurrCmd = new MySqlCommand(@"
                                INSERT INTO curriculum (course_id, year_level, section)
                                VALUES (@cid, @yl, @sec);
                                SELECT LAST_INSERT_ID();", conn);
                            newCurrCmd.Parameters.AddWithValue("@cid", courseId);
                            newCurrCmd.Parameters.AddWithValue("@yl",  yearInt);
                            newCurrCmd.Parameters.AddWithValue("@sec", section);
                            curriculumId = Convert.ToInt32(newCurrCmd.ExecuteScalar());
                        }
                    }
                }

                // Ensure students row exists, then update it
                var ensureCmd = new MySqlCommand(@"
                    INSERT IGNORE INTO students (user_id, student_number)
                    VALUES (@uid, @sn)", conn);
                ensureCmd.Parameters.AddWithValue("@uid", userId);
                ensureCmd.Parameters.AddWithValue("@sn",  studentNumber);
                ensureCmd.ExecuteNonQuery();

                var updateStuCmd = new MySqlCommand(@"
                    UPDATE students SET
                        student_number = @sn,
                        curriculum_id  = @cid
                    WHERE user_id = @uid", conn);
                updateStuCmd.Parameters.AddWithValue("@sn",  studentNumber);
                updateStuCmd.Parameters.AddWithValue("@cid",
                    curriculumId > 0 ? curriculumId : DBNull.Value);
                updateStuCmd.Parameters.AddWithValue("@uid", userId);
                updateStuCmd.ExecuteNonQuery();

                TempData["ProfileSaved"] = "Profile updated successfully!";
            }
            catch (Exception ex)
            {
                TempData["ProfileSaved"] = "Error: " + ex.Message;
            }

            return RedirectToAction(nameof(Profile));
        }

        // ── Save Signature (AJAX) ─────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveSignature([FromBody] SaveSignatureDto dto)
        {
            var userId = int.Parse(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var cmd = new MySqlCommand(
                    "UPDATE student_signatories SET signature_data = @sd WHERE user_id = @uid", conn);
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

        // ── Download PDF ──────────────────────────────────────
        public IActionResult DownloadPdf()
        {
            SetUserViewData();

            var userId = int.Parse(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var model = new StudentClearancePdfViewModel();

            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                var infoCmd = new MySqlCommand(@"
                    SELECT
                        CONCAT(u.first_name, ' ', u.last_name) AS full_name,
                        s.student_number,
                        c.course_code,
                        cu.year_level
                    FROM users u
                    LEFT JOIN students   s  ON s.user_id  = u.id
                    LEFT JOIN curriculum cu ON cu.id       = s.curriculum_id
                    LEFT JOIN courses    c  ON c.id        = cu.course_id
                    WHERE u.id = @uid LIMIT 1", conn);
                infoCmd.Parameters.AddWithValue("@uid", userId);

                using var ir = infoCmd.ExecuteReader();
                if (ir.Read())
                {
                    model.StudentName = ir.IsDBNull(ir.GetOrdinal("full_name"))
                                        ? "" : ir.GetString("full_name");
                    model.StudentId   = ir.IsDBNull(ir.GetOrdinal("student_number"))
                                        ? "" : ir.GetString("student_number");

                    var course  = ir.IsDBNull(ir.GetOrdinal("course_code"))
                                    ? "" : ir.GetString("course_code");
                    var yl      = ir.IsDBNull(ir.GetOrdinal("year_level"))
                                    ? 0  : ir.GetInt32("year_level");
                    var ylLabel = yl switch
                    {
                        1 => "1st Year", 2 => "2nd Year",
                        3 => "3rd Year", _ => $"{yl}th Year"
                    };
                    model.CourseYear = $"{course} – {ylLabel}";
                }
                ir.Close();

                var periodCmd = new MySqlCommand(
                    "SELECT CONCAT(academic_year, ' / ', semester) " +
                    "FROM academic_periods WHERE is_active = 1 LIMIT 1", conn);
                var periodLabel = periodCmd.ExecuteScalar()?.ToString();
                model.AySemester = !string.IsNullOrEmpty(periodLabel)
                                    ? periodLabel : "2025-2026 / 2nd Semester";

                var snCmd = new MySqlCommand(
                    "SELECT student_number FROM students WHERE user_id = @uid LIMIT 1", conn);
                snCmd.Parameters.AddWithValue("@uid", userId);
                var studentNumber = snCmd.ExecuteScalar()?.ToString() ?? "";

                var subjCmd = new MySqlCommand(@"
                    SELECT
                        cs.mis_code                                     AS MisCode,
                        COALESCE(s.subject_code, cs.mis_code)           AS SubjectCode,
                        COALESCE(s.title, '—')                          AS Description,
                        COALESCE(
                            CONCAT(u.first_name, ' ', u.last_name),
                            'TBA'
                        )                                               AS InstructorName,
                        COALESCE(st.label, 'Pending')                   AS Status
                    FROM clearance_subjects cs
                    LEFT JOIN subject_offerings  so  ON so.mis_code     = cs.mis_code
                    LEFT JOIN subjects           s   ON s.subject_code  = so.subject_code
                    LEFT JOIN signatories        sig ON sig.employee_id = so.instructor_id
                    LEFT JOIN users              u   ON u.id            = sig.user_id
                    LEFT JOIN status_table       st  ON st.id           = cs.status
                    WHERE cs.student_number = @sn
                    ORDER BY cs.id", conn);
                subjCmd.Parameters.AddWithValue("@sn", studentNumber);

                using var sr = subjCmd.ExecuteReader();
                while (sr.Read())
                {
                    model.Subjects.Add(new PdfSubjectItem
                    {
                        MisCode        = sr.GetString("MisCode"),
                        SubjectCode    = sr.GetString("SubjectCode"),
                        Description    = sr.GetString("Description"),
                        InstructorName = sr.GetString("InstructorName"),
                        Status         = sr.GetString("Status")
                    });
                }
                sr.Close();

                var currCmd = new MySqlCommand(
                    "SELECT curriculum_id FROM students WHERE user_id = @uid LIMIT 1", conn);
                currCmd.Parameters.AddWithValue("@uid", userId);
                var curriculumId = Convert.ToInt32(currCmd.ExecuteScalar() ?? 0);

                var orgCmd = new MySqlCommand(@"
                    SELECT
                        o.position_title               AS Role,
                        o.org_signatory                AS PersonName,
                        COALESCE(st.label, 'None')     AS Status
                    FROM organizations o
                    LEFT JOIN clearance_organization co
                           ON co.org_name       = o.org_name
                          AND co.student_number = @sn
                    LEFT JOIN status_table st ON st.id = co.status
                    WHERE o.curriculum_id = @cid
                    ORDER BY o.id", conn);
                orgCmd.Parameters.AddWithValue("@sn",  studentNumber);
                orgCmd.Parameters.AddWithValue("@cid", curriculumId);

                using var or2 = orgCmd.ExecuteReader();
                while (or2.Read())
                {
                    model.Organizations.Add(new PdfOrganizationItem
                    {
                        Role       = or2.IsDBNull(or2.GetOrdinal("Role"))
                                        ? "—" : or2.GetString("Role"),
                        PersonName = or2.IsDBNull(or2.GetOrdinal("PersonName"))
                                        ? "—" : or2.GetString("PersonName"),
                        Status     = or2.GetString("Status")
                    });
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Could not load PDF data: " + ex.Message;
            }

            return View(model);
        }

        // ── Private helpers ───────────────────────────────────
        private void SetUserViewData()
        {
            var firstName   = User.FindFirst("FirstName")?.Value ?? "";
            var lastName    = User.FindFirst("LastName")?.Value  ?? "";
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0";

            ViewData["Email"]    = $"{firstName} {lastName}".Trim();
            ViewData["UserId"]      = "—";
            ViewData["UserCourse"]  = "—";
            ViewData["UserYear"]    = "—";
            ViewData["UserSection"] = "";

            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();
                var uid = int.Parse(userIdClaim);

                var cmd = new MySqlCommand(@"
                    SELECT
                        s.student_number,
                        u.id_number,
                        c.course_code,
                        cu.year_level,
                        cu.section
                    FROM users u
                    LEFT JOIN students   s  ON s.user_id  = u.id
                    LEFT JOIN curriculum cu ON cu.id       = s.curriculum_id
                    LEFT JOIN courses    c  ON c.id        = cu.course_id
                    WHERE u.id = @uid LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@uid", uid);

                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    var studentNum = r.IsDBNull(r.GetOrdinal("student_number"))
                                        ? null : r.GetString("student_number");
                    var idNumber   = r.IsDBNull(r.GetOrdinal("id_number"))
                                        ? null : r.GetString("id_number");
                    ViewData["UserId"] = studentNum ?? idNumber ?? "—";

                    ViewData["UserCourse"] = r.IsDBNull(r.GetOrdinal("course_code"))
                                                ? "—" : r.GetString("course_code");

                    if (!r.IsDBNull(r.GetOrdinal("year_level")))
                    {
                        var yl = r.GetInt32("year_level");
                        ViewData["UserYear"] = yl switch
                        {
                            1 => "1st Year",
                            2 => "2nd Year",
                            3 => "3rd Year",
                            _ => $"{yl}th Year"
                        };
                    }

                    ViewData["UserSection"] = r.IsDBNull(r.GetOrdinal("section"))
                                                ? "" : r.GetString("section");
                }
            }
            catch { /* use default fallbacks */ }
        }
    }

    // ── DTOs ─────────────────────────────────────────────────
    public class RequestSubjectDto
    {
        public string? MisCode { get; set; }
    }

    public class RequestOrgDto
    {
        public string? OrgName { get; set; }
    }

    public class SaveSignatureDto
    {
        public string? SignatureData { get; set; }
    }
}
