using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Crest.Users.ViewModels;

public class ResetAuthenticatorViewModel
{
    [BindNever]
    public bool WillDisableTwoFactor { get; set; }

    [BindNever]
    public bool CanRemove { get; internal set; }
}
