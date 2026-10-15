using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Grove.Users.ViewModels;

public class RequestCodeSmsAuthenticatorViewModel
{
    public string PhoneNumber { get; set; }

    [BindNever]
    public bool AllowChangingPhoneNumber { get; set; }
}
