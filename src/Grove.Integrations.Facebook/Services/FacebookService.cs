using System.Text.Json.Nodes;
using Grove.Data.Connectors;
using Grove.Integrations.Facebook.Settings;
using Grove.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Integrations.Facebook.Services;

public class FacebookService : IFacebookService
{
    private readonly ISiteService _siteService;
    private readonly IServiceProvider _services;

    public FacebookService(ISiteService siteService, IServiceProvider services)
    {
        _siteService = siteService;
        _services = services;
    }

    public Task<FacebookSettings> GetSettingsAsync()
        => _siteService.GetSettingsAsync<FacebookSettings>();

    public async Task UpdateSettingsAsync(FacebookSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var container = await _siteService.LoadSiteSettingsAsync();
        container.Properties[nameof(FacebookSettings)] = JObject.FromObject(settings);
        await _siteService.UpdateSiteSettingsAsync(container);
    }

    public async Task<string> GetAppIdAsync()
    {
        using var scope = _services.CreateScope();
        var provider = scope.ServiceProvider.GetService<IAppCredentialsProvider>();
        if (provider is null)
        {
            return null;
        }

        return (await provider.GetAsync(FacebookConstants.AppCredentialsKey))?.ClientId;
    }
}
