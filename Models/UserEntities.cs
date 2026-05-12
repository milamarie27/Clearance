using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineClearanceSystem.Models
{
    [Table("users")]
    public class User
    {
        [Key] public int Id { get; set; }

        [Required, MaxLength(50)]
        public string FirstName { get; set; } = "";

        [MaxLength(10)]
        public string MiddleInitial { get; set; } = "";

        [Required, MaxLength(50)]
        public string LastName { get; set; } = "";

        [MaxLength(20)]
        public string SuffixName { get; set; } = "";

        [Required, MaxLength(100), EmailAddress]
        public string Email { get; set; } = "";

        [Required, MaxLength(255)]
        public string Password { get; set; } = "";

        [MaxLength(50)]
        public string? IdNumber { get; set; }

        [Required, MaxLength(20)]
        public string Role { get; set; } = "Pending";

        public bool     IsActive  { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Student-specific fields (null for non-students)
        [MaxLength(50)]
        public string? StudentNumber { get; set; }

        public int? CurriculumId { get; set; }

        // Navigation
        [ForeignKey("CurriculumId")]
        public Curriculum? Curriculum { get; set; }

        public UserSignature? Signature { get; set; }

        [NotMapped]
        public string FullName =>
            $"{FirstName} {(string.IsNullOrEmpty(MiddleInitial) ? "" : MiddleInitial + ". ")}{LastName} {SuffixName}".Trim();
    }

    [Table("user_signatures")]
    public class UserSignature
    {
        [Key] public int Id { get; set; }

        [Required] public int UserId { get; set; }

        // NULL for instructors/staff; filled for student officers
        [MaxLength(100)]
        public string? Position { get; set; }

        public string? SignatureData { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }
    }
}
