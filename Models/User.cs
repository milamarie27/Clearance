using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineClearanceSystem.Models
{
    // ✅ NO [Table] attribute
    public class User
    {
        public int    Id        { get; set; }
        public string Username  { get; set; } = "";
        public string Password  { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName  { get; set; } = "";
        public string Role      { get; set; } = "Pending"; // ✅ string, NOT enum, NOT nullable
        public bool   IsActive  { get; set; } = false;
        public string? IdNumber { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public string FullName => $"{FirstName} {LastName}".Trim();
    }
}