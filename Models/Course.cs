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

        public bool IsActive  { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        public ICollection<Student>    Students   { get; set; } = new List<Student>();
        public ICollection<Curriculum> Curricula  { get; set; } = new List<Curriculum>();
    }
}