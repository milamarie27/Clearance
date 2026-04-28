using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineClearanceSystem.Models
{
    [Table("users")]
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Username  { get; set; } = "";

        [Required, MaxLength(255)]
        public string Password  { get; set; } = "";   // BCrypt hash

        [Required, MaxLength(50)]
        public string FirstName { get; set; } = "";

        [Required, MaxLength(50)]
        public string LastName  { get; set; } = "";

        [MaxLength(20)]
        public string? Role     { get; set; }         // Admin | null (student/instructor)

        public bool IsActive    { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // ── Computed (not stored in DB) ────────────────────────
        [NotMapped]
        public string FullName => $"{FirstName} {LastName}".Trim();
    }
}