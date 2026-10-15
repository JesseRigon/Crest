using Grove.Data.Documents;
using Grove.Security;

namespace Grove.Access.Roles.Models;

/// <summary>The tenant's permission sets, one cached document like the roles.</summary>
public class PermissionSetsDocument : Document
{
    public List<PermissionSet> Sets { get; set; } = [];
}
