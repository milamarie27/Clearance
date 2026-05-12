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
        public string Status { get; set; } = "Pending";  // Pending|Cleared|Declined

        [MaxLength(20)]
        public string AcademicYear { get; set; } = "2025-2026";

        [MaxLength(10)]
        public string Semester { get; set; } = "2nd";

        public DateTime  RequestedAt { get; set; } = DateTime.Now;
        public DateTime? ActionAt    { get; set; }

        [ForeignKey("SubjectOfferingId")]
        public SubjectOffering? SubjectOffering { get; set; }
    }

    [Table("clearance_organization")]
    public class ClearanceOrganization
    {
        [Key] public int Id { get; set; }

        [MaxLength(50)]
        public string StudentNumber { get; set; } = "";

        // Stores position_title value (e.g. "SSG President", "Class Adviser")
        [MaxLength(200)]
        public string Position { get; set; } = "";

        [MaxLength(20)]
        public string Status { get; set; } = "Pending";  // Pending|Cleared|Declined

        public DateTime  CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    [Table("organizations")]
    public class Organization
    {
        [Key] public int Id { get; set; }

        [Required, MaxLength(200)]
        public string PositionTitle { get; set; } = "";

        public int? UserId       { get; set; }  // Signatory user ID
        public int? CurriculumId { get; set; }  // NULL = school-wide; NOT NULL = section-specific adviser
        public bool IsActive     { get; set; } = true;

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [ForeignKey("CurriculumId")]
        public Curriculum? Curriculum { get; set; }
    }
}
