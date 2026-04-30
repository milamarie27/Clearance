namespace OnlineClearanceSystem.Models
{
    public class StudentClearancePdfViewModel
    {
        public string StudentName { get; set; } = "";
        public string StudentId   { get; set; } = "";
        public string CourseYear  { get; set; } = "";
        public List<PdfSubjectItem>      SubjectClearances      { get; set; } = new();
        public List<PdfOrganizationItem> OrganizationClearances { get; set; } = new();
    }

    public class PdfSubjectItem
    {
        public string Code   { get; set; } = "";
        public string Subj   { get; set; } = "";
        public string Inst   { get; set; } = "";
        public string Status { get; set; } = "";
    }

    public class PdfOrganizationItem
    {
        public int    Num    { get; set; }
        public string Role   { get; set; } = "";
        public string Person { get; set; } = "";
        public string Status { get; set; } = "";
    }
}