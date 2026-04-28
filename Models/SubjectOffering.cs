using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineClearanceSystem.Models
{
    [Table("subject_offerings")]
    public class SubjectOffering
    {
        [Key] public int Id { get; set; }

        public int SubjectId    { get; set; }
        public int InstructorId { get; set; }

        [MaxLength(20)] public string AcademicYear { get; set; } = "2025-2026";
        [MaxLength(10)] public string Semester     { get; set; } = "2nd";
        [MaxLength(10)] public string Section      { get; set; } = "";

        public bool IsActive  { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("SubjectId")]
        public Subject? Subject { get; set; }
    }
}