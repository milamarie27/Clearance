using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineClearanceSystem.Models;

namespace OnlineClearanceSystem.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        // ── Dashboard ─────────────────────────────────────────
        public IActionResult Dashboard()
        {
            SetUserViewData();
            var model = new StudentDashboardViewModel
            {
                StudentName       = ViewData["UserName"]?.ToString() ?? "Student",
                SubjectCleared    = 3,
                SubjectIncomplete = 0,
                OrgCleared        = 0
            };
            return View(model);
        }

        // ── Subjects Offered ──────────────────────────────────
        public IActionResult SubjectsOffered()
        {
            SetUserViewData();
            return View(new SubjectOfferedViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ConfirmSubjects(string selectedSubjects)
        {
            // TODO: save selectedSubjects to DB
            TempData["ConfirmedSubjects"] = selectedSubjects;
            return RedirectToAction(nameof(Clearance));
        }

        // ── Clearance ─────────────────────────────────────────
        public IActionResult Clearance()
        {
            SetUserViewData();
            // TODO: load real data from DB
            return View(new List<StudentClearanceItem>());
        }

        // ── Organization ──────────────────────────────────────
        public IActionResult Organization()
        {
            SetUserViewData();
            // TODO: load real data from DB
            return View(new List<OrganizationSignatory>());
        }

        // ── Profile ───────────────────────────────────────────
        public IActionResult Profile()
        {
            SetUserViewData();
            var model = new StudentProfileViewModel
            {
                StudentId = ViewData["UserId"]?.ToString() ?? "",
                Username  = User.Identity?.Name ?? ""
            };
            return View(model);
        }

        // ── Download PDF ──────────────────────────────────────
        public IActionResult DownloadPdf()
        {
            SetUserViewData();
            var model = new StudentClearancePdfViewModel
            {
                StudentName = ViewData["UserName"]?.ToString() ?? "",
                StudentId   = ViewData["UserId"]?.ToString()   ?? "",
                CourseYear  = $"{ViewData["UserCourse"]} – {ViewData["UserYear"]}"
            };
            return View(model);
        }

        // ── Helper: populate sidebar ViewData ─────────────────
        private void SetUserViewData()
        {
            ViewData["UserName"]   = User.FindFirst("FirstName")?.Value + " "
                                   + User.FindFirst(System.Security.Claims.ClaimTypes.Surname)?.Value;
            ViewData["UserId"]     = User.FindFirst(
                                       System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
            ViewData["UserCourse"] = "BSIT";
            ViewData["UserYear"]   = "2nd Year";
        }
    }
}