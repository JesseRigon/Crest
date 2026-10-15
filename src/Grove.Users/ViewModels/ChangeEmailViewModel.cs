using System.ComponentModel.DataAnnotations;

namespace Grove.Users.ViewModels;

public class ChangeEmailViewModel
{
    [Required(ErrorMessage = "Email is required.")]
    [Email.EmailAddress(ErrorMessage = "Invalid Email.")]
    public string Email { get; set; }
}
