using Grove.Data.Documents;
using Grove.Environment.Shell.Models;

namespace Grove.Infrastructure.Tenants.Models;

public class FeatureProfilesDocument : Document
{
    public Dictionary<string, FeatureProfile> FeatureProfiles { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
