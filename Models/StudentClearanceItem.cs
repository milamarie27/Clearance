namespace OnlineClearanceSystem.Models
{
    public class StudentClearanceItem
    {
        public required string MisCode        { get; set; }
        public required string SubjectCode    { get; set; }
        public required string Description    { get; set; }
        public required string InstructorName { get; set; }
        public required string Status         { get; set; }
    }
}