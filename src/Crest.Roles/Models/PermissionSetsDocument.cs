using Crest.Data.Documents;
using Crest.Security;

namespace Crest.Roles.Models;

/// <summary>The tenant's permission sets, one cached document like the roles.</summary>
public class PermissionSetsDocument : Document
{
    public List<PermissionSet> Sets { get; set; } = [];
}
