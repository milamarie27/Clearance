using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineClearanceSystem.Models
{
    [Table("clearance_subjects")]
    public class ClearanceSubject
    {
        [Key] public int Id { get; set; }

        public int StudentId         { get; set; }
        public int SubjectOfferingId { get; set; }
        public int InstructorId      { get; set; }

        [MaxLength(20)]
        public string Status      { get; set; } = "Pending"; // Pending|Approved|Declined

        [MaxLength(20)]
        public string AcademicYear{ get; set; } = "2025-2026";

        [MaxLength(10)]
        public string Semester    { get; set; } = "2nd";

        public DateTime  RequestedAt { get; set; } = DateTime.Now;
        public DateTime? ActionAt    { get; set; }

        // Navigation
        [ForeignKey("StudentId")]
        public Student?         Student         { get; set; }

        [ForeignKey("SubjectOfferingId")]
        public SubjectOffering? SubjectOffering  { get; set; }
    }
}