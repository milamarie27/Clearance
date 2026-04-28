using System.ComponentModel.DataAnnotations;

namespace OnlineClearanceSystem.Models
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "First name is required.")]
        [Display(Name = "First Name")]
        public string FirstName       { get; set; } = "";

        [Display(Name = "Middle Initial")]
        [MaxLength(3)]
        public string? MiddleInitial  { get; set; }

        [Required(ErrorMessage = "Last name is required.")]
        [Display(Name = "Last Name")]
        public string LastName        { get; set; } = "";

        [Required(ErrorMessage = "ID Number is required.")]
        [Display(Name = "ID Number")]
        public string IdNumber        { get; set; } = "";

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6,
            ErrorMessage = "Minimum 6 characters.")]
        public string Password        { get; set; } = "";

        [Required(ErrorMessage = "Please confirm your password.")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = "";
    }
}