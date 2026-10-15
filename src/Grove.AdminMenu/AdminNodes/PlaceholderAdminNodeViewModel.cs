using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Grove.AdminMenu.AdminNodes;

public class PlaceholderAdminNodeViewModel
{
    [Required]
    public string LinkText { get; set; }

    public string IconClass { get; set; }

    public string SelectedPermissionNames { get; set; }

    [BindNever]
    public IList<PermissionViewModel> SelectedItems { get; set; }

    [BindNever]
    public IList<PermissionViewModel> AllItems { get; set; }
}
