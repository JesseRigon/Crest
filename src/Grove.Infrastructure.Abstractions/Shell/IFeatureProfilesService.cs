using Grove.Environment.Shell.Models;

namespace Grove.Environment.Shell;

public interface IFeatureProfilesService
{
    Task<IDictionary<string, FeatureProfile>> GetFeatureProfilesAsync();
}
