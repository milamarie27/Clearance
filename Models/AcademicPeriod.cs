using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineClearanceSystem.Models
{
    [Table("academic_periods")]
    public class AcademicPeriod
    {
        [Key] public int Id { get; set; }

        [Required, MaxLength(20)]
        public string YearLabel { get; set; } = "";   // e.g. 2025-2026

        [Required, MaxLength(10)]
        public string Semester  { get; set; } = "2nd";

        public bool     IsActive  { get; set; } = false;
        public DateTime StartDate { get; set; }
        public DateTime EndDate   { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}