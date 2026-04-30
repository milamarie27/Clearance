namespace OnlineClearanceSystem.Models
{
    public class StudentDashboardViewModel
    {
        public required string StudentName      { get; set; }
        public int             SubjectCleared   { get; set; }
        public int             SubjectIncomplete { get; set; }
        public int             OrgCleared       { get; set; }
    }
}