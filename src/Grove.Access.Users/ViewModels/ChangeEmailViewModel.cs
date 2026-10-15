using System.ComponentModel.DataAnnotations;

namespace Grove.Access.Users.ViewModels;

public class ChangeEmailViewModel
{
    [Required(ErrorMessage = "Email is required.")]
    [Grove.Communications.Email.EmailAddress(ErrorMessage = "Invalid Email.")]
    public string Email { get; set; }
}
