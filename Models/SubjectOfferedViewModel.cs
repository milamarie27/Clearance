namespace OnlineClearanceSystem.Models
{
    public class SubjectOfferedViewModel
    {
        public List<SubjectItem> AvailableSubjects { get; set; } = new();
    }

    public class SubjectItem
    {
        public string Id             { get; set; } = "";
        public string MisCode        { get; set; } = "";
        public string SubjectCode    { get; set; } = "";
        public string Description    { get; set; } = "";
        public string InstructorName { get; set; } = "";
    }
}