using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Crest.Users.ViewModels;

public class EditUserPhoneNumberViewModel
{
    public string PhoneNumber { get; set; }

    [BindNever]
    public bool PhoneNumberConfirmed { get; set; }

    [BindNever]
    public bool AllowEditing { get; set; }
}

