using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.Security;

namespace Crest.Roles.ViewModels;

public class CloneRoleViewModel
{
    public string Name { get; set; }

    public string RoleDescription { get; set; }

    [BindNever]
    public Role Role { get; set; }

    [BindNever]
    public RolePermissionsViewModel Permissions { get; set; }
}
