using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineClearanceSystem.Models
{
    [Table("signatories")]
    public class Signatory
    {
        [Key] public int Id { get; set; }

        public int UserId       { get; set; }
        public int? OrganizationId { get; set; }

        [Required, MaxLength(100)]
        public string Position  { get; set; } = ""; // SSG Treasurer, Campus Director

        public int  SortOrder  { get; set; } = 0;
        public bool IsActive   { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("OrganizationId")]
        public Organization? Organization { get; set; }
    }
}