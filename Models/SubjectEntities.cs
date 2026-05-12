using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineClearanceSystem.Models
{
    [Table("subjects")]
    public class Subject
    {
        [Key] public int Id { get; set; }

        [Required, MaxLength(20)]  public string MisCode     { get; set; } = "";
        [Required, MaxLength(50)]  public string SubjectCode { get; set; } = "";
        [Required, MaxLength(200)] public string Description { get; set; } = "";

        public int  LecUnit  { get; set; } = 2;
        public int  LabUnit  { get; set; } = 2;
        public bool IsActive { get; set; } = true;
    }

    [Table("subject_offerings")]
    public class SubjectOffering
    {
        [Key] public int Id { get; set; }

        [Required] public int SubjectId { get; set; }
        [Required] public int UserId    { get; set; }  // Instructor/Signatory user ID
        [Required] public int PeriodId  { get; set; }

        [MaxLength(50)]
        public string MisCode  { get; set; } = "";
        public bool   IsActive { get; set; } = true;

        // Navigation
        [ForeignKey("SubjectId")]
        public Subject? Subject { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [ForeignKey("PeriodId")]
        public AcademicPeriod? Period { get; set; }

        [NotMapped] public string SubjectCode => Subject?.SubjectCode ?? "";
        [NotMapped] public string Description => Subject?.Description ?? "";
        [NotMapped] public int    LabUnit      => Subject?.LabUnit     ?? 0;
        [NotMapped] public int    LecUnit      => Subject?.LecUnit     ?? 0;
    }
}
