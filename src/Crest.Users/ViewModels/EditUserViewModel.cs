using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Crest.Users.ViewModels;

public class EditUserViewModel
{
    public string Password { get; set; }

    public string PasswordConfirmation { get; set; }

    [BindNever]
    public bool IsNewRequest { get; set; }
}
