using Crest.Data.Documents;
using Crest.Security;

namespace Crest.Roles.Models;

public class RolesDocument : Document
{
    public List<Role> Roles { get; set; } = [];
    public Dictionary<string, List<string>> MissingFeaturesByRole { get; set; } = [];
}
