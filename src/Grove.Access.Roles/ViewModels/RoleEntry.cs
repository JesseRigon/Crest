using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Grove.Access.Roles.ViewModels;

public class RoleEntry
{
    public string Name { get; set; }

    public string Description { get; set; }

    [BindNever]
    public bool IsSystemRole { get; set; }

    [BindNever]
    public bool IsAdminRole { get; set; }
}
