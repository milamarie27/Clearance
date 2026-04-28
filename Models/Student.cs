using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineClearanceSystem.Models
{
    [Table("students")]
    public class Student
    {
        [Key] public int Id { get; set; }

        [Required, MaxLength(50)]  public string FirstName     { get; set; } = "";
        [MaxLength(10)]            public string MiddleInitial { get; set; } = "";
        [Required, MaxLength(50)]  public string LastName      { get; set; } = "";
        [MaxLength(20)]            public string Suffix        { get; set; } = "";
        [Required, MaxLength(50)]  public string StudentId     { get; set; } = ""; // School ID
        [Required, MaxLength(255)] public string PasswordHash  { get; set; } = "";

        public int CourseId { get; set; }

        [MaxLength(10)]  public string YearLevel { get; set; } = "1st";
        [MaxLength(10)]  public string Section   { get; set; } = "";
        [MaxLength(20)]  public string Status    { get; set; } = "Regular"; // Regular|Irregular

        [MaxLength(20)]  public string? AdditionalRole { get; set; } // Staff or null

        public bool     IsActive  { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("CourseId")]
        public Course? Course { get; set; }

        [NotMapped]
        public string FullName => $"{FirstName} {(string.IsNullOrEmpty(MiddleInitial) ? "" : MiddleInitial + ". ")}{LastName}".Trim();

        [NotMapped]
        public bool IsStaff => AdditionalRole == "Staff";
    }
}