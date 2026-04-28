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

        public int LecUnit { get; set; } = 2;
        public int LabUnit { get; set; } = 2;

        public bool IsActive  { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}