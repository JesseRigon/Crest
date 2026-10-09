using Crest.Environment.Shell.Models;

namespace Crest.Environment.Shell;

public interface IFeatureProfilesService
{
    Task<IDictionary<string, FeatureProfile>> GetFeatureProfilesAsync();
}
