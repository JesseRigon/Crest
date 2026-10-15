using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Grove.Users.ViewModels;

public class ResetAuthenticatorViewModel
{
    [BindNever]
    public bool WillDisableTwoFactor { get; set; }

    [BindNever]
    public bool CanRemove { get; internal set; }
}
