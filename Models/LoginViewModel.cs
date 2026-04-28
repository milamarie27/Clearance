using System.ComponentModel.DataAnnotations;

namespace OnlineClearanceSystem.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "ID Number is required.")]
        [Display(Name = "ID Number")]
        public string IdNumber   { get; set; } = "";

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password   { get; set; } = "";

        [Display(Name = "Remember me")]
        public bool RememberMe   { get; set; } = true;
    }
}