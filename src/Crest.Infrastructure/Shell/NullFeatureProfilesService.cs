using Crest.Environment.Shell.Models;

namespace Crest.Environment.Shell;

public class NullFeatureProfilesService : IFeatureProfilesService
{
    private static readonly IDictionary<string, FeatureProfile> s_featureProfiles = new Dictionary<string, FeatureProfile>();

    public Task<IDictionary<string, FeatureProfile>> GetFeatureProfilesAsync()
        => Task.FromResult(s_featureProfiles);
}
