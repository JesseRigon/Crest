using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.Security;

namespace Crest.Roles.ViewModels;

public class DisplayRoleViewModel
{
    [BindNever]
    public Role Role { get; set; }

    [BindNever]
    public RolePermissionsViewModel Permissions { get; set; }
}
