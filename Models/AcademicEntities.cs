using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineClearanceSystem.Models
{
    [Table("courses")]
    public class Course
    {
        [Key] public int Id { get; set; }

        [Required, MaxLength(100)]
        public string CourseName { get; set; } = "";  // Bachelor of Science in IT

        [Required, MaxLength(20)]
        public string CourseCode { get; set; } = "";  // BSIT

        public bool IsActive { get; set; } = true;

        public ICollection<Curriculum> Curricula { get; set; } = new List<Curriculum>();
    }

    [Table("curriculum")]
    public class Curriculum
    {
        [Key] public int Id { get; set; }

        [Required] public int CourseId  { get; set; }
        [Required] public int YearLevel { get; set; } = 1;

        [Required, MaxLength(20)]
        public string Section { get; set; } = "";  // just the letter, e.g. "A"

        [ForeignKey("CourseId")]
        public Course? Course { get; set; }
    }

    [Table("sections")]
    public class Section
    {
        [Key] public int Id { get; set; }

        public int CourseId { get; set; }

        [Required, MaxLength(20)]
        public string SectionName { get; set; } = "";  // just the letter, e.g. "A"

        public int YearLevel { get; set; }

        public bool IsActive { get; set; } = true;

        [ForeignKey("CourseId")]
        public Course? Course { get; set; }
    }

    [Table("academic_periods")]
    public class AcademicPeriod
    {
        [Key] public int Id { get; set; }

        [Required, MaxLength(20)]
        public string YearLabel { get; set; } = "";  // e.g. 2025-2026

        [Required, MaxLength(10)]
        public string Semester { get; set; } = "2nd";

        public bool     IsActive  { get; set; } = false;
        public DateTime StartDate { get; set; }
        public DateTime EndDate   { get; set; }
    }
}
