using Grove.Data.Documents;
using Grove.Security;

namespace Grove.Roles.Models;

public class RolesDocument : Document
{
    public List<Role> Roles { get; set; } = [];
    public Dictionary<string, List<string>> MissingFeaturesByRole { get; set; } = [];
}
