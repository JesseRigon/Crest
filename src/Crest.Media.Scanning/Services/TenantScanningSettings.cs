using Crest.Settings;

namespace Crest.Media.Scanning.Services;

/// <summary>The tenant's scanning choice, from its site settings.</summary>
public interface ITenantScanningSettings
{
    Task<UploadScanningSettings> GetAsync();
}

public sealed class SiteTenantScanningSettings(ISiteService siteService) : ITenantScanningSettings
{
    public Task<UploadScanningSettings> GetAsync() => siteService.GetSettingsAsync<UploadScanningSettings>();
}
