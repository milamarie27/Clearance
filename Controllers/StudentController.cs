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

        // ── Dashboard ─────────────────────────────────────────────────────
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
                    "SELECT student_number FROM users WHERE id = @uid LIMIT 1", conn);
                snCmd.Parameters.AddWithValue("@uid", userId);
                var studentNumber = snCmd.ExecuteScalar()?.ToString() ?? "";

                var periodCmd = new MySqlCommand(
                    "SELECT CONCAT('A.Y. ', year_label, ', ', semester) " +
                    "FROM academic_periods WHERE is_active = 1 LIMIT 1", conn);
                var period = periodCmd.ExecuteScalar()?.ToString();
                if (!string.IsNullOrEmpty(period)) model.ActivePeriod = period;

                var subjCmd = new MySqlCommand(@"
                    SELECT
                        COUNT(*)                                                    AS total,
                        SUM(CASE WHEN status = 'Cleared'  THEN 1 ELSE 0 END)       AS cleared,
                        SUM(CASE WHEN status != 'Cleared' THEN 1 ELSE 0 END)       AS incomplete
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
                        COUNT(*)                                                   AS total,
                        SUM(CASE WHEN co.status = 'Cleared' THEN 1 ELSE 0 END)    AS cleared
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
                    SELECT title, body AS content, type, posted_at AS created_at
                    FROM announcements
                    ORDER BY posted_at DESC
                    LIMIT 10", conn);

                using var ar = annCmd.ExecuteReader();
                while (ar.Read())
                {
                    model.Announcements.Add(new AnnouncementItem
                    {
                        Title   = ar.GetString("title"),
                        Content = ar.GetString("content"),
                        Type    = ar.IsDBNull(ar.GetOrdinal("type")) ? "General" : ar.GetString("type"),
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

        // ── Subjects Offered ──────────────────────────────────────────────
        public IActionResult SubjectsOffered()
        {
            SetUserViewData();
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var model = new SubjectOfferedViewModel();

            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                var periodCmd = new MySqlCommand(
                    "SELECT CONCAT('A.Y. ', year_label, ', ', semester) " +
                    "FROM academic_periods WHERE is_active = 1 LIMIT 1", conn);
                var period = periodCmd.ExecuteScalar()?.ToString();
                if (!string.IsNullOrEmpty(period)) model.ActivePeriod = period;

                var snCmd = new MySqlCommand(
                    "SELECT student_number FROM users WHERE id = @uid LIMIT 1", conn);
                snCmd.Parameters.AddWithValue("@uid", userId);
                var studentNumber = snCmd.ExecuteScalar()?.ToString() ?? "";

                var cmd = new MySqlCommand(@"
                    SELECT
                        so.mis_code                                     AS Id,
                        so.mis_code                                     AS MisCode,
                        s.subject_code                                  AS SubjectCode,
                        s.description                                   AS Description,
                        COALESCE(CONCAT(u.first_name, ' ', u.last_name), 'TBA') AS InstructorName,
                        COALESCE(cs.status, '')                         AS EnrolledStatus
                    FROM subject_offerings  so
                    JOIN      subjects      s   ON s.id        = so.subject_id
                    LEFT JOIN users         u   ON u.id        = so.user_id
                    LEFT JOIN clearance_subjects cs
                           ON cs.mis_code       = so.mis_code
                          AND cs.student_number = @sn
                    WHERE so.is_active = 1
                    ORDER BY s.subject_code", conn);
                cmd.Parameters.AddWithValue("@sn", studentNumber);

                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var enrolledStatus = r.IsDBNull(r.GetOrdinal("EnrolledStatus"))
                                            ? "" : r.GetString("EnrolledStatus");
                    model.AvailableSubjects.Add(new SubjectItem
                    {
                        Id              = r.GetString("Id"),
                        MisCode         = r.GetString("MisCode"),
                        SubjectCode     = r.GetString("SubjectCode"),
                        Description     = r.GetString("Description"),
                        InstructorName  = r.GetString("InstructorName"),
                        AlreadyEnrolled = !string.IsNullOrEmpty(enrolledStatus),
                        EnrolledStatus  = enrolledStatus
                    });
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Could not load subjects: " + ex.Message;
            }

            return View(model);
        }

        // ── Confirm Subjects POST ─────────────────────────────────────────
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
                    "SELECT student_number FROM users WHERE id = @uid LIMIT 1", conn);
                snCmd.Parameters.AddWithValue("@uid", userId);
                var studentNumber = snCmd.ExecuteScalar()?.ToString() ?? "";

                if (string.IsNullOrEmpty(studentNumber))
                {
                    TempData["Error"] = "Student record not found.";
                    return RedirectToAction(nameof(SubjectsOffered));
                }

                var periodCmd = new MySqlCommand(
                    "SELECT id FROM academic_periods WHERE is_active = 1 LIMIT 1", conn);
                var periodId = Convert.ToInt32(periodCmd.ExecuteScalar() ?? 1);

                foreach (var code in selectedSubjects.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var insertCmd = new MySqlCommand(@"
                        INSERT INTO clearance_subjects
                            (student_number, mis_code, status, period_id)
                        SELECT @sn, @mc, 'Pending', @pid
                        WHERE NOT EXISTS (
                            SELECT 1 FROM clearance_subjects
                            WHERE student_number = @sn AND mis_code = @mc
                        )", conn);
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

        // ── Clearance ─────────────────────────────────────────────────────
    public IActionResult Clearance()
{
    SetUserViewData();

    var userId = int.Parse(
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    var model = new StudentClearanceViewModel();

    try
    {
        using var conn = DbHelper.GetConnection(_config);
        conn.Open();

        // ── Active period label ───────────────────────────────────────────
        var periodCmd = new MySqlCommand(
            "SELECT CONCAT('A.Y. ', year_label, ', ', semester) " +
            "FROM academic_periods WHERE is_active = 1 LIMIT 1", conn);
        var period = periodCmd.ExecuteScalar()?.ToString();
        if (!string.IsNullOrEmpty(period)) ViewData["ActivePeriod"] = period;

        // ── Resolve student_number + curriculum_id ────────────────────────
        var stuCmd = new MySqlCommand(
            "SELECT student_number, curriculum_id FROM users WHERE id = @uid LIMIT 1", conn);
        stuCmd.Parameters.AddWithValue("@uid", userId);

        string studentNumber = "";
        int    curriculumId  = 0;

        using (var r = stuCmd.ExecuteReader())
        {
            if (r.Read())
            {
                studentNumber = r.IsDBNull(r.GetOrdinal("student_number"))
                    ? "" : r.GetString("student_number");
                curriculumId = r.IsDBNull(r.GetOrdinal("curriculum_id"))
                    ? 0 : r.GetInt32("curriculum_id");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // PART A — Subject Clearance rows
        // ════════════════════════════════════════════════════════════════════
        var subjCmd = new MySqlCommand(@"
            SELECT
                cs.mis_code                                                     AS MisCode,
                COALESCE(s.subject_code, cs.mis_code)                          AS SubjectCode,
                COALESCE(s.description, '—')                                   AS Description,
                COALESCE(CONCAT(u.first_name,' ',u.last_name), 'TBA')          AS InstructorName,
                COALESCE(cs.status, 'Pending')                                 AS Status
            FROM clearance_subjects cs
            LEFT JOIN subject_offerings so  ON so.mis_code COLLATE utf8mb4_unicode_ci = cs.mis_code COLLATE utf8mb4_unicode_ci
            LEFT JOIN subjects          s   ON s.id        = so.subject_id
            LEFT JOIN users             u   ON u.id        = so.user_id
            WHERE cs.student_number COLLATE utf8mb4_unicode_ci = @sn
            ORDER BY cs.mis_code", conn);
        subjCmd.Parameters.Add(new MySqlParameter("@sn", MySqlDbType.VarChar) { Value = studentNumber });

        using (var r = subjCmd.ExecuteReader())
        {
            while (r.Read())
            {
                model.SubjectItems.Add(new StudentClearanceItem
                {
                    MisCode        = r.GetString("MisCode"),
                    SubjectCode    = r.GetString("SubjectCode"),
                    Description    = r.GetString("Description"),
                    InstructorName = r.GetString("InstructorName"),
                    Status         = r.GetString("Status")
                });
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // PART B — Class Adviser
        // ════════════════════════════════════════════════════════════════════
        if (curriculumId > 0)
        {
            var advCmd = new MySqlCommand(@"
                SELECT
                    CONCAT(u.first_name, ' ', u.last_name) AS AdviserName,
                    c.course_code                          AS Course,
                    cu.year_level                          AS YearLevel,
                    cu.section                             AS Section,
                    co.status                              AS CoStatus
                FROM   organizations o
                JOIN   users      u   ON u.id   = o.user_id
                JOIN   curriculum cu  ON cu.id  = o.curriculum_id
                JOIN   courses    c   ON c.id   = cu.course_id
                LEFT JOIN clearance_organization co
                       ON co.position       COLLATE utf8mb4_unicode_ci = 'Class Adviser'
                      AND co.student_number COLLATE utf8mb4_unicode_ci = @sn
                WHERE  o.curriculum_id  = @cid
                  AND  o.position_title COLLATE utf8mb4_unicode_ci = 'Class Adviser'
                  AND  o.is_active      = 1
                LIMIT  1", conn);

            advCmd.Parameters.Add(new MySqlParameter("@sn", MySqlDbType.VarChar) { Value = studentNumber });
            advCmd.Parameters.AddWithValue("@cid", curriculumId);

            using var advRdr = advCmd.ExecuteReader();
            if (advRdr.Read())
            {
                var yl      = advRdr.IsDBNull(advRdr.GetOrdinal("YearLevel")) ? 0  : advRdr.GetInt32("YearLevel");
                var ylLabel = yl switch { 1 => "1st Year", 2 => "2nd Year", 3 => "3rd Year", _ => $"{yl}th Year" };
                var coStatus = advRdr.IsDBNull(advRdr.GetOrdinal("CoStatus")) ? "" : advRdr.GetString("CoStatus");
                var course   = advRdr.IsDBNull(advRdr.GetOrdinal("Course"))   ? "" : advRdr.GetString("Course");
                var section  = advRdr.IsDBNull(advRdr.GetOrdinal("Section"))  ? "" : advRdr.GetString("Section");

                model.ClassAdviser = new OrganizationSignatory
                {
                    OrgName         = "Class Adviser",
                    OrgRole         = $"{course} — {ylLabel}{(string.IsNullOrEmpty(section) ? "" : $", Section {section}")}",
                    PersonName      = advRdr.IsDBNull(advRdr.GetOrdinal("AdviserName")) ? "—" : advRdr.GetString("AdviserName"),
                    Status          = string.IsNullOrEmpty(coStatus) ? "" : coStatus,
                    IsSelfSignatory = false
                };
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // PART C — ALL active org signatory rows except Class Adviser
        //           (removed the curriculum_id IS NULL filter that was hiding rows)
        // ════════════════════════════════════════════════════════════════════
        if (!string.IsNullOrEmpty(studentNumber))
        {
            var orgCmd = new MySqlCommand(@"
                SELECT
                    o.position_title                        AS OrgRole,
                    CONCAT(u.first_name, ' ', u.last_name) AS PersonName,
                    o.user_id                              AS SignatoryUserId,
                    co.status                              AS CoStatus
                FROM   organizations o
                LEFT JOIN users u  ON u.id = o.user_id
                LEFT JOIN clearance_organization co
                       ON  co.position       COLLATE utf8mb4_unicode_ci = o.position_title COLLATE utf8mb4_unicode_ci
                      AND  co.student_number COLLATE utf8mb4_unicode_ci = @sn
                WHERE  o.is_active      = 1
                  AND  o.position_title COLLATE utf8mb4_unicode_ci != 'Class Adviser'
                ORDER BY o.position_title", conn);

            orgCmd.Parameters.Add(new MySqlParameter("@sn", MySqlDbType.VarChar) { Value = studentNumber });

            using var or = orgCmd.ExecuteReader();
            while (or.Read())
            {
                var signatoryUserId = or.IsDBNull(or.GetOrdinal("SignatoryUserId")) ? 0  : or.GetInt32("SignatoryUserId");
                var coStatus        = or.IsDBNull(or.GetOrdinal("CoStatus"))        ? "" : or.GetString("CoStatus");
                var role            = or.IsDBNull(or.GetOrdinal("OrgRole"))         ? "" : or.GetString("OrgRole");

                model.OrgItems.Add(new OrganizationSignatory
                {
                    OrgName         = role,
                    OrgRole         = role,
                    PersonName      = or.IsDBNull(or.GetOrdinal("PersonName")) ? "—" : or.GetString("PersonName"),
                    Status          = coStatus,
                    IsSelfSignatory = signatoryUserId == userId
                });
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // PART D — Positions the student personally holds (self-signatory)
        //           Only add if not already present from Part C to avoid duplicates
        // ════════════════════════════════════════════════════════════════════
        var ssCmd = new MySqlCommand(@"
            SELECT
                us.position                             AS OrgRole,
                CONCAT(u.first_name, ' ', u.last_name) AS PersonName,
                co.status                              AS CoStatus
            FROM   user_signatures us
            JOIN   users u ON u.id = us.user_id
            LEFT JOIN clearance_organization co
                   ON  co.position       COLLATE utf8mb4_unicode_ci = us.position COLLATE utf8mb4_unicode_ci
                  AND  co.student_number COLLATE utf8mb4_unicode_ci = @sn
            WHERE  us.user_id   = @uid
              AND  us.position IS NOT NULL", conn);

        ssCmd.Parameters.Add(new MySqlParameter("@sn", MySqlDbType.VarChar) { Value = studentNumber });
        ssCmd.Parameters.AddWithValue("@uid", userId);

        using var ssr = ssCmd.ExecuteReader();
        while (ssr.Read())
        {
            var coStatus = ssr.IsDBNull(ssr.GetOrdinal("CoStatus")) ? "" : ssr.GetString("CoStatus");
            var role     = ssr.IsDBNull(ssr.GetOrdinal("OrgRole"))  ? "" : ssr.GetString("OrgRole");

            // Skip if Part C already added this position (student is the assigned signatory)
            if (model.OrgItems.Any(x => x.OrgName.Equals(role, StringComparison.OrdinalIgnoreCase)))
                continue;

            model.OrgItems.Add(new OrganizationSignatory
            {
                OrgName         = role,
                OrgRole         = role,
                PersonName      = ssr.IsDBNull(ssr.GetOrdinal("PersonName")) ? "—" : ssr.GetString("PersonName"),
                Status          = coStatus,
                IsSelfSignatory = true
            });
        }
    }
    catch (Exception ex)
    {
        TempData["Error"] = "Could not load clearance: " + ex.Message;
    }

    return View(model);
}

// Redirect old /Student/Organization URLs to the merged page
public IActionResult Organization() => RedirectToAction(nameof(Clearance));
        // ── Request Subject Signature (AJAX POST) ─────────────────────────
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
                    "SELECT student_number FROM users WHERE id = @uid LIMIT 1", conn);
                snCmd.Parameters.AddWithValue("@uid", userId);
                var studentNumber = snCmd.ExecuteScalar()?.ToString() ?? "";

                if (string.IsNullOrEmpty(studentNumber))
                    return Json(new { success = false, error = "Student record not found." });

                var periodCmd = new MySqlCommand(
                    "SELECT id FROM academic_periods WHERE is_active = 1 LIMIT 1", conn);
                var periodId = Convert.ToInt32(periodCmd.ExecuteScalar() ?? 1);

                var checkCmd = new MySqlCommand(@"
                    SELECT status FROM clearance_subjects
                    WHERE student_number = @sn AND mis_code = @mis
                    LIMIT 1", conn);
                checkCmd.Parameters.AddWithValue("@sn",  studentNumber);
                checkCmd.Parameters.AddWithValue("@mis", dto.MisCode);
                var existing = checkCmd.ExecuteScalar();

                if (existing != null && existing != DBNull.Value)
                {
                    var existingStatus = existing.ToString() ?? "";
                    if (existingStatus == "Pending")
                        return Json(new { success = false, error = "Request already pending for this subject." });
                    if (existingStatus == "Cleared")
                        return Json(new { success = false, error = "This subject is already cleared." });
                }

                var insertCmd = new MySqlCommand(@"
                    INSERT INTO clearance_subjects
                        (student_number, mis_code, status, period_id)
                    VALUES (@sn, @mis, 'Pending', @pid)
                    ON DUPLICATE KEY UPDATE status = 'Pending'", conn);
                insertCmd.Parameters.AddWithValue("@sn",  studentNumber);
                insertCmd.Parameters.AddWithValue("@mis", dto.MisCode);
                insertCmd.Parameters.AddWithValue("@pid", periodId);
                insertCmd.ExecuteNonQuery();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── Request Org Signature POST ────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult RequestOrgSignature([FromBody] RequestOrgDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.OrgName))
                return Json(new { success = false, error = "Invalid request." });

            var userId = int.Parse(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                var stuCmd = new MySqlCommand(
                    "SELECT student_number, curriculum_id FROM users WHERE id = @uid LIMIT 1", conn);
                stuCmd.Parameters.AddWithValue("@uid", userId);

                string studentNumber = "";
                int curriculumId = 0;

                using (var r = stuCmd.ExecuteReader())
                {
                    if (!r.Read())
                        return Json(new { success = false, error = "Student record not found." });
                    studentNumber = r.IsDBNull(r.GetOrdinal("student_number")) ? "" : r.GetString("student_number");
                    curriculumId  = r.IsDBNull(r.GetOrdinal("curriculum_id"))  ? 0  : r.GetInt32("curriculum_id");
                }

                // Check if this is a valid org position
                var checkOrgCmd = new MySqlCommand(@"
                    SELECT COUNT(*) FROM organizations
                    WHERE  position_title = @pos
                      AND  is_active = 1
                      AND  (curriculum_id IS NULL OR curriculum_id = @cid)", conn);
                checkOrgCmd.Parameters.AddWithValue("@pos", dto.OrgName);
                checkOrgCmd.Parameters.AddWithValue("@cid", curriculumId);
                var orgExists = Convert.ToInt32(checkOrgCmd.ExecuteScalar()) > 0;

                // Check if the student holds this position themselves (student signatory)
                var checkSsCmd = new MySqlCommand(@"
                    SELECT COUNT(*) FROM user_signatures
                    WHERE  user_id   = @uid
                      AND  position  = @pos", conn);
                checkSsCmd.Parameters.AddWithValue("@uid", userId);
                checkSsCmd.Parameters.AddWithValue("@pos", dto.OrgName);
                var isSelfPosition = Convert.ToInt32(checkSsCmd.ExecuteScalar()) > 0;

                if (!orgExists && !isSelfPosition)
                    return Json(new { success = false, error = "You are not allowed to request this position." });

                var existCmd = new MySqlCommand(@"
                    SELECT status FROM clearance_organization
                    WHERE  student_number = @sn
                      AND  position       = @pos
                    LIMIT  1", conn);
                existCmd.Parameters.AddWithValue("@sn",  studentNumber);
                existCmd.Parameters.AddWithValue("@pos", dto.OrgName);
                var existStatus = existCmd.ExecuteScalar();

                if (existStatus != null && existStatus != DBNull.Value)
                {
                    var st = existStatus.ToString() ?? "";
                    if (st == "Pending") return Json(new { success = false, error = "Request already pending." });
                    if (st == "Cleared") return Json(new { success = false, error = "Already cleared." });

                    var resetCmd = new MySqlCommand(@"
                        UPDATE clearance_organization
                        SET    status = 'Pending'
                        WHERE  student_number = @sn
                          AND  position       = @pos", conn);
                    resetCmd.Parameters.AddWithValue("@sn",  studentNumber);
                    resetCmd.Parameters.AddWithValue("@pos", dto.OrgName);
                    resetCmd.ExecuteNonQuery();

                    return Json(new { success = true });
                }

                var insertCmd = new MySqlCommand(@"
                    INSERT INTO clearance_organization
                        (student_number, position, status)
                    VALUES (@sn, @pos, 'Pending')", conn);
                insertCmd.Parameters.AddWithValue("@sn",  studentNumber);
                insertCmd.Parameters.AddWithValue("@pos", dto.OrgName);
                insertCmd.ExecuteNonQuery();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── Self-Approve / Decline Org Signature ──────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult SelfApproveOrg([FromBody] SelfApproveOrgDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.OrgName))
                return Json(new { success = false, error = "Invalid request." });

            var userId = int.Parse(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            try
            {
                using var conn = DbHelper.GetConnection(_config);
                conn.Open();

                var verifyCmd = new MySqlCommand(@"
                    SELECT COUNT(*) FROM user_signatures
                    WHERE  user_id  = @uid
                      AND  position = @pos", conn);
                verifyCmd.Parameters.AddWithValue("@uid", userId);
                verifyCmd.Parameters.AddWithValue("@pos", dto.OrgName);
                if (Convert.ToInt32(verifyCmd.ExecuteScalar()) == 0)
                    return Json(new { success = false, error = "You do not hold this position." });

                var snCmd = new MySqlCommand(
                    "SELECT student_number FROM users WHERE id = @uid LIMIT 1", conn);
                snCmd.Parameters.AddWithValue("@uid", userId);
                var studentNumber = snCmd.ExecuteScalar()?.ToString() ?? "";

                if (string.IsNullOrEmpty(studentNumber))
                    return Json(new { success = false, error = "Student record not found." });

                var checkCmd = new MySqlCommand(@"
                    SELECT status FROM clearance_organization
                    WHERE  student_number = @sn
                      AND  position       = @pos
                    LIMIT  1", conn);
                checkCmd.Parameters.AddWithValue("@sn",  studentNumber);
                checkCmd.Parameters.AddWithValue("@pos", dto.OrgName);
                var existing = checkCmd.ExecuteScalar();

                if (existing == null || existing == DBNull.Value)
                    return Json(new { success = false, error = "No pending request found. Press Request first." });

                if (existing.ToString() != "Pending")
                    return Json(new { success = false, error = "Request is not in Pending state." });

                var newStatus = dto.Approve ? "Cleared" : "Declined";

                var updateCmd = new MySqlCommand(@"
                    UPDATE clearance_organization
                    SET    status = @st
                    WHERE  student_number = @sn
                      AND  position       = @pos", conn);
                updateCmd.Parameters.AddWithValue("@st",  newStatus);
                updateCmd.Parameters.AddWithValue("@sn",  studentNumber);
                updateCmd.Parameters.AddWithValue("@pos", dto.OrgName);
                updateCmd.ExecuteNonQuery();

                return Json(new { success = true, newStatus });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── Profile GET ───────────────────────────────────────────────────
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

                var coursesCmd = new MySqlCommand(
                    "SELECT course_code FROM courses WHERE is_active = 1 ORDER BY course_code", conn);
                using (var cr = coursesCmd.ExecuteReader())
                {
                    while (cr.Read())
                        model.AvailableCourses.Add(cr.GetString("course_code"));
                }

                var secCmd = new MySqlCommand(@"
                    SELECT s.section_name, s.year_level, c.course_code
                    FROM   sections s
                    JOIN   courses  c ON c.id = s.course_id
                    WHERE  s.is_active = 1
                    ORDER BY c.course_code, s.year_level, s.section_name", conn);
                using (var secR = secCmd.ExecuteReader())
                {
                    while (secR.Read())
                    {
                        model.AvailableSections.Add(new SectionItem
                        {
                            SectionName = secR.GetString("section_name"),
                            YearLevel   = secR.GetInt32("year_level"),
                            CourseCode  = secR.GetString("course_code")
                        });
                    }
                }

                var cmd = new MySqlCommand(@"
                    SELECT
                        u.first_name, u.middle_initial,
                        u.last_name,  u.suffix_name, u.email,
                        u.id_number,  u.student_number,
                        u.curriculum_id,
                        c.course_code,
                        cu.year_level,
                        cu.section
                    FROM users u
                    LEFT JOIN curriculum cu ON cu.id = u.curriculum_id
                    LEFT JOIN courses    c  ON c.id  = cu.course_id
                    WHERE u.id = @uid LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@uid", userId);

                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        var studentNum = r.IsDBNull(r.GetOrdinal("student_number")) ? null : r.GetString("student_number");
                        var idNumber   = r.IsDBNull(r.GetOrdinal("id_number"))      ? null : r.GetString("id_number");

                        model.StudentId     = studentNum ?? idNumber ?? "";
                        model.FirstName     = r.IsDBNull(r.GetOrdinal("first_name"))     ? "" : r.GetString("first_name");
                        model.MiddleInitial = r.IsDBNull(r.GetOrdinal("middle_initial")) ? "" : r.GetString("middle_initial");
                        model.LastName      = r.IsDBNull(r.GetOrdinal("last_name"))      ? "" : r.GetString("last_name");
                        model.Suffix        = r.IsDBNull(r.GetOrdinal("suffix_name"))    ? "" : r.GetString("suffix_name");
                        model.Email         = r.IsDBNull(r.GetOrdinal("email"))          ? "" : r.GetString("email");
                        model.Course        = r.IsDBNull(r.GetOrdinal("course_code"))    ? "" : r.GetString("course_code");
                        model.Section       = r.IsDBNull(r.GetOrdinal("section"))        ? "" : r.GetString("section");
                        model.Password      = "";

                        if (!r.IsDBNull(r.GetOrdinal("year_level")))
                        {
                            model.YearLevel = r.GetInt32("year_level") switch
                            {
                                1 => "1st Year", 2 => "2nd Year",
                                3 => "3rd Year", _ => "4th Year"
                            };
                        }
                    }
                }

                var posCmd = new MySqlCommand(@"
                    SELECT position AS role_name
                    FROM   user_signatures
                    WHERE  user_id   = @uid
                      AND  position IS NOT NULL

                    UNION

                    SELECT position_title AS role_name
                    FROM   organizations
                    WHERE  user_id   = @uid
                      AND  is_active = 1

                    ORDER BY role_name", conn);
                posCmd.Parameters.AddWithValue("@uid", userId);

                using (var pr = posCmd.ExecuteReader())
                {
                    while (pr.Read())
                    {
                        model.Positions.Add(new OrganizationSignatory
                        {
                            OrgRole    = pr.IsDBNull(0) ? "" : pr.GetString(0),
                        });
                    }
                }

                var signatureCmd = new MySqlCommand(@"
                    SELECT signature_data
                    FROM   user_signatures
                    WHERE  user_id        = @uid
                      AND  signature_data IS NOT NULL
                      AND  signature_data != ''
                    LIMIT  1", conn);
                signatureCmd.Parameters.AddWithValue("@uid", userId);
                var signatureResult = signatureCmd.ExecuteScalar();
                if (signatureResult != null && signatureResult != DBNull.Value)
                    model.SignaturePath = signatureResult.ToString();
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Error loading profile: " + ex.Message;
            }

            return View(model);
        }

        // ── Profile POST ──────────────────────────────────────────────────
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

                if (!string.IsNullOrWhiteSpace(model.Password))
                {
                    var hash = BCrypt.Net.BCrypt.HashPassword(model.Password);
                    var cmd  = new MySqlCommand(@"
                        UPDATE users SET
                            first_name = @fn, middle_initial = @mi,
                            last_name  = @ln, suffix_name    = @sx,
                            email      = @em, password       = @pw
                        WHERE id = @id", conn);
                    cmd.Parameters.AddWithValue("@fn", model.FirstName?.Trim()     ?? "");
                    cmd.Parameters.AddWithValue("@mi", model.MiddleInitial?.Trim() ?? "");
                    cmd.Parameters.AddWithValue("@ln", model.LastName?.Trim()      ?? "");
                    cmd.Parameters.AddWithValue("@sx", model.Suffix?.Trim()        ?? "");
                    cmd.Parameters.AddWithValue("@em", model.Email?.Trim()         ?? "");
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
                            email      = @em
                        WHERE id = @id", conn);
                    cmd.Parameters.AddWithValue("@fn", model.FirstName?.Trim()     ?? "");
                    cmd.Parameters.AddWithValue("@mi", model.MiddleInitial?.Trim() ?? "");
                    cmd.Parameters.AddWithValue("@ln", model.LastName?.Trim()      ?? "");
                    cmd.Parameters.AddWithValue("@sx", model.Suffix?.Trim()        ?? "");
                    cmd.Parameters.AddWithValue("@em", model.Email?.Trim()         ?? "");
                    cmd.Parameters.AddWithValue("@id", userId);
                    cmd.ExecuteNonQuery();
                }

                var studentNumber = model.StudentId?.Trim() ?? "";
                var courseCode    = model.Course?.Trim()    ?? "";
                var section       = model.Section?.Trim()   ?? "";
                var yearInt = model.YearLevel switch
                {
                    "1st Year" => 1, "2nd Year" => 2,
                    "3rd Year" => 3, "4th Year" => 4, _ => 0
                };

                int curriculumId = 0;
                if (!string.IsNullOrEmpty(courseCode) && yearInt > 0)
                {
                    var courseCmd = new MySqlCommand(
                        "SELECT id FROM courses WHERE course_code = @c LIMIT 1", conn);
                    courseCmd.Parameters.AddWithValue("@c", courseCode);
                    var courseId = Convert.ToInt32(courseCmd.ExecuteScalar() ?? 0);

                    if (courseId > 0)
                    {
                        var findCmd = new MySqlCommand(@"
                            SELECT id FROM curriculum
                            WHERE course_id  = @cid
                              AND year_level = @yl
                              AND section    = @sec
                            LIMIT 1", conn);
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

                // Student fields now live on the users table
                var updateUserCmd = new MySqlCommand(@"
                    UPDATE users SET
                        student_number = @sn,
                        curriculum_id  = @cid
                    WHERE id = @uid", conn);
                updateUserCmd.Parameters.AddWithValue("@sn",  studentNumber);
                updateUserCmd.Parameters.AddWithValue("@cid",
                    curriculumId > 0 ? (object)curriculumId : DBNull.Value);
                updateUserCmd.Parameters.AddWithValue("@uid", userId);
                updateUserCmd.ExecuteNonQuery();

                TempData["ProfileSaved"] = "Profile updated successfully!";
            }
            catch (Exception ex)
            {
                TempData["ProfileSaved"] = "Error: " + ex.Message;
            }

            return RedirectToAction(nameof(Profile));
        }

        // ── Save Signature (AJAX) ─────────────────────────────────────────
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

                var cmd = new MySqlCommand(@"
                    UPDATE user_signatures
                    SET    signature_data = @sd
                    WHERE  user_id = @uid", conn);
                cmd.Parameters.AddWithValue("@sd",  dto.SignatureData ?? "");
                cmd.Parameters.AddWithValue("@uid", userId);
                cmd.ExecuteNonQuery();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── Download PDF ──────────────────────────────────────────────────
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
                        u.student_number,
                        c.course_code,
                        cu.year_level
                    FROM users u
                    LEFT JOIN curriculum cu ON cu.id = u.curriculum_id
                    LEFT JOIN courses    c  ON c.id  = cu.course_id
                    WHERE u.id = @uid LIMIT 1", conn);
                infoCmd.Parameters.AddWithValue("@uid", userId);

                string studentNumber = "";
                int curriculumId = 0;

                using var ir = infoCmd.ExecuteReader();
                if (ir.Read())
                {
                    model.StudentName = ir.IsDBNull(ir.GetOrdinal("full_name"))      ? "" : ir.GetString("full_name");
                    model.StudentId   = ir.IsDBNull(ir.GetOrdinal("student_number")) ? "" : ir.GetString("student_number");
                    studentNumber     = model.StudentId;

                    var course  = ir.IsDBNull(ir.GetOrdinal("course_code")) ? "" : ir.GetString("course_code");
                    var yl      = ir.IsDBNull(ir.GetOrdinal("year_level"))  ? 0  : ir.GetInt32("year_level");
                    var ylLabel = yl switch { 1 => "1st Year", 2 => "2nd Year", 3 => "3rd Year", _ => $"{yl}th Year" };
                    model.CourseYear = $"{course} – {ylLabel}";
                }
                ir.Close();

                var currCmd = new MySqlCommand(
                    "SELECT curriculum_id FROM users WHERE id = @uid LIMIT 1", conn);
                currCmd.Parameters.AddWithValue("@uid", userId);
                curriculumId = Convert.ToInt32(currCmd.ExecuteScalar() ?? 0);

                var periodCmd = new MySqlCommand(
                    "SELECT CONCAT(year_label, ' / ', semester) " +
                    "FROM academic_periods WHERE is_active = 1 LIMIT 1", conn);
                model.AySemester = periodCmd.ExecuteScalar()?.ToString() ?? "2025-2026 / 2nd Semester";

                var subjCmd = new MySqlCommand(@"
                    SELECT
                        cs.mis_code                                                     AS MisCode,
                        COALESCE(s.subject_code, cs.mis_code)                          AS SubjectCode,
                        COALESCE(s.description, '—')                                   AS Description,
                        COALESCE(CONCAT(u.first_name,' ',u.last_name), 'TBA')          AS InstructorName,
                        COALESCE(cs.status, 'Pending')                                 AS Status,
                        COALESCE(sig.signature_data, '')                               AS SignatureBase64
                    FROM clearance_subjects cs
                    LEFT JOIN subject_offerings so  ON so.mis_code  = cs.mis_code
                    LEFT JOIN subjects          s   ON s.id         = so.subject_id
                    LEFT JOIN users             u   ON u.id         = so.user_id
                    LEFT JOIN user_signatures   sig ON sig.user_id  = so.user_id
                                                   AND sig.position IS NULL
                    WHERE cs.student_number = @sn
                    ORDER BY cs.mis_code", conn);
                subjCmd.Parameters.AddWithValue("@sn", studentNumber);

                using var sr = subjCmd.ExecuteReader();
                while (sr.Read())
                {
                    model.Subjects.Add(new PdfSubjectItem
                    {
                        MisCode         = sr.IsDBNull(sr.GetOrdinal("MisCode"))         ? "" : sr.GetString("MisCode"),
                        SubjectCode     = sr.IsDBNull(sr.GetOrdinal("SubjectCode"))     ? "" : sr.GetString("SubjectCode"),
                        Description     = sr.IsDBNull(sr.GetOrdinal("Description"))     ? "" : sr.GetString("Description"),
                        InstructorName  = sr.IsDBNull(sr.GetOrdinal("InstructorName"))  ? "" : sr.GetString("InstructorName"),
                        Status          = sr.IsDBNull(sr.GetOrdinal("Status"))          ? "" : sr.GetString("Status"),
                        SignatureBase64 = sr.IsDBNull(sr.GetOrdinal("SignatureBase64")) ? "" : sr.GetString("SignatureBase64")
                    });
                }
                sr.Close();

                var orgCmd = new MySqlCommand(@"
                    SELECT
                        o.position_title                                            AS OrgName,
                        o.position_title                                            AS Role,
                        COALESCE(CONCAT(u.first_name, ' ', u.last_name), 'TBA')    AS PersonName,
                        COALESCE(co.status, 'None')                                AS Status,
                        COALESCE(sig.signature_data, '')                            AS SignatureBase64,
                        0                                                           AS IsSelfSignatory
                    FROM organizations o
                    LEFT JOIN users             u   ON u.id          = o.user_id
                    LEFT JOIN user_signatures   sig ON sig.user_id   = o.user_id
                                                    AND sig.position IS NULL
                    LEFT JOIN clearance_organization co
                           ON co.position      = o.position_title
                          AND co.student_number = @sn
                    WHERE o.is_active = 1
                      AND (o.curriculum_id IS NULL OR o.curriculum_id = @cid)

                    UNION ALL

                    SELECT
                        us.position                                                 AS OrgName,
                        us.position                                                 AS Role,
                        COALESCE(CONCAT(u2.first_name, ' ', u2.last_name), 'TBA')  AS PersonName,
                        COALESCE(co2.status, 'None')                               AS Status,
                        COALESCE(us.signature_data, '')                            AS SignatureBase64,
                        1                                                           AS IsSelfSignatory
                    FROM user_signatures us
                    JOIN  users u2 ON u2.id = us.user_id
                    LEFT JOIN clearance_organization co2
                           ON co2.position      = us.position
                          AND co2.student_number = @sn
                    WHERE us.user_id    = @uid
                      AND us.position IS NOT NULL

                    ORDER BY OrgName", conn);

                orgCmd.Parameters.AddWithValue("@sn",  studentNumber);
                orgCmd.Parameters.AddWithValue("@cid", curriculumId > 0 ? (object)curriculumId : DBNull.Value);
                orgCmd.Parameters.AddWithValue("@uid", userId);

                using var or2 = orgCmd.ExecuteReader();
                while (or2.Read())
                {
                    model.Organizations.Add(new PdfOrganizationItem
                    {
                        OrgName         = or2.IsDBNull(or2.GetOrdinal("OrgName"))         ? "—" : or2.GetString("OrgName"),
                        Role            = or2.IsDBNull(or2.GetOrdinal("Role"))            ? "—" : or2.GetString("Role"),
                        PersonName      = or2.IsDBNull(or2.GetOrdinal("PersonName"))      ? "—" : or2.GetString("PersonName"),
                        Status          = or2.IsDBNull(or2.GetOrdinal("Status"))          ? "None" : or2.GetString("Status"),
                        SignatureBase64 = or2.IsDBNull(or2.GetOrdinal("SignatureBase64")) ? ""     : or2.GetString("SignatureBase64"),
                        IsSelfSignatory = or2.GetInt32("IsSelfSignatory") == 1
                    });
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Could not load PDF data: " + ex.Message;
            }

            return View(model);
        }

        // ── Private helpers ───────────────────────────────────────────────
        private void SetUserViewData()
        {
            var firstName   = User.FindFirst("FirstName")?.Value ?? "";
            var lastName    = User.FindFirst("LastName")?.Value  ?? "";
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0";

            ViewData["Email"]       = $"{firstName} {lastName}".Trim();
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
                        u.student_number,
                        u.id_number,
                        c.course_code,
                        cu.year_level,
                        cu.section
                    FROM users u
                    LEFT JOIN curriculum cu ON cu.id = u.curriculum_id
                    LEFT JOIN courses    c  ON c.id  = cu.course_id
                    WHERE u.id = @uid LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@uid", uid);

                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    var studentNum = r.IsDBNull(r.GetOrdinal("student_number")) ? null : r.GetString("student_number");
                    var idNumber   = r.IsDBNull(r.GetOrdinal("id_number"))      ? null : r.GetString("id_number");
                    ViewData["UserId"] = studentNum ?? idNumber ?? "—";

                    ViewData["UserCourse"] = r.IsDBNull(r.GetOrdinal("course_code"))
                                                ? "—" : r.GetString("course_code");

                    if (!r.IsDBNull(r.GetOrdinal("year_level")))
                    {
                        ViewData["UserYear"] = r.GetInt32("year_level") switch
                        {
                            1 => "1st Year", 2 => "2nd Year",
                            3 => "3rd Year", _ => $"{r.GetInt32("year_level")}th Year"
                        };
                    }

                    ViewData["UserSection"] = r.IsDBNull(r.GetOrdinal("section"))
                                                ? "" : r.GetString("section");
                }
            }
            catch { }
        }
    }

    // ── DTOs ──────────────────────────────────────────────────────────────
    public class RequestSubjectDto  { public string? MisCode { get; set; } }
    public class SelfApproveOrgDto  { public string? OrgName { get; set; } public bool Approve { get; set; } }
}
