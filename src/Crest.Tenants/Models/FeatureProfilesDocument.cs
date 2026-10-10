using Crest.Data.Documents;
using Crest.Environment.Shell.Models;

namespace Crest.Tenants.Models;

public class FeatureProfilesDocument : Document
{
    public Dictionary<string, FeatureProfile> FeatureProfiles { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
