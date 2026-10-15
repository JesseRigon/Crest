using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Grove.Users.ViewModels;

public class EditUserNameViewModel
{
    [Required]
    public string UserName { get; set; }

    [BindNever]
    public bool AllowEditing { get; set; }
}
