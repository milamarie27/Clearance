using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineClearanceSystem.Models
{
    [Table("announcements")]
    public class Announcement
    {
        [Key] public int Id { get; set; }

        public int PostedById { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = "";

        [Required]
        public string Body { get; set; } = "";

        [MaxLength(20)]
        public string Type { get; set; } = "general";  // general|urgent|event|reminder

        public bool     IsPinned { get; set; } = false;
        public bool     IsActive { get; set; } = true;
        public DateTime PostedAt { get; set; } = DateTime.Now;
    }
}
