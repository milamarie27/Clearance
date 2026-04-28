using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineClearanceSystem.Models
{
    [Table("organizations")]
    public class Organization
    {
        [Key] public int Id { get; set; }

        [Required, MaxLength(100)]
        public string OrgName    { get; set; } = "";

        [MaxLength(200)]
        public string Description{ get; set; } = "";

        public bool IsActive  { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        public ICollection<Signatory> Signatories { get; set; } = new List<Signatory>();
    }
}