using Grove.Environment.Shell;
using Grove.Environment.Shell.Models;

namespace Grove.Infrastructure.Tenants.Services;

public class FeatureProfilesService : IFeatureProfilesService
{
    private readonly FeatureProfilesManager _featureProfilesManager;

    public FeatureProfilesService(FeatureProfilesManager featureProfilesManager)
    {
        _featureProfilesManager = featureProfilesManager;
    }

    public async Task<IDictionary<string, FeatureProfile>> GetFeatureProfilesAsync()
    {
        var document = await _featureProfilesManager.LoadFeatureProfilesDocumentAsync();

        return document.FeatureProfiles;
    }
}
