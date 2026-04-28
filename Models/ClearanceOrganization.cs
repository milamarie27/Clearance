using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineClearanceSystem.Models
{
    [Table("clearance_organization")]
    public class ClearanceOrganization
    {
        [Key] public int Id { get; set; }

        public int StudentId     { get; set; }
        public int SignatoryId   { get; set; }
        public int OrganizationId{ get; set; }

        [MaxLength(20)]
        public string Status      { get; set; } = "Pending"; // Pending|Approved|Declined

        [MaxLength(20)]
        public string AcademicYear{ get; set; } = "2025-2026";

        [MaxLength(10)]
        public string Semester    { get; set; } = "2nd";

        public DateTime  RequestedAt { get; set; } = DateTime.Now;
        public DateTime? ActionAt    { get; set; }

        // Navigation
        [ForeignKey("StudentId")]
        public Student?      Student      { get; set; }

        [ForeignKey("SignatoryId")]
        public Signatory?    Signatory    { get; set; }

        [ForeignKey("OrganizationId")]
        public Organization? Organization { get; set; }
    }
}