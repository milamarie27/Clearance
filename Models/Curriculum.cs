using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineClearanceSystem.Models
{
    [Table("curriculum")]
    public class Curriculum
    {
        [Key] public int Id { get; set; }

        public int CourseId  { get; set; }
        public int SubjectId { get; set; }

        [MaxLength(10)]
        public string YearLevel { get; set; } = "1st"; // 1st|2nd|3rd|4th

        [MaxLength(10)]
        public string Semester  { get; set; } = "1st"; // 1st|2nd

        public bool IsActive  { get; set; } = true;

        // Navigation
        [ForeignKey("CourseId")]
        public Course?  Course  { get; set; }

        [ForeignKey("SubjectId")]
        public Subject? Subject { get; set; }
    }
}