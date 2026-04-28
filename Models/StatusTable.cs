using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineClearanceSystem.Models
{
    [Table("status_table")]
    public class StatusTable
    {
        [Key] public int Id { get; set; }

        [Required, MaxLength(50)]
        public string StatusName { get; set; } = ""; // Pending, Approved, Declined, Cleared

        [MaxLength(20)]
        public string StatusCode { get; set; } = "";

        [MaxLength(20)]
        public string StatusType { get; set; } = ""; // subject|organization|general

        [MaxLength(20)]
        public string Color      { get; set; } = ""; // green|red|orange (for UI badges)
    }
}