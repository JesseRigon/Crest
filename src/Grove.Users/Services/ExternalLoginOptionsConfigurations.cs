using Microsoft.Extensions.Options;
using Grove.Settings;
using Grove.Users.Models;

namespace Grove.Users.Services;

public sealed class ExternalLoginOptionsConfigurations : IConfigureOptions<ExternalLoginOptions>
{
    private readonly ISiteService _siteService;

    public ExternalLoginOptionsConfigurations(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public void Configure(ExternalLoginOptions options)
    {
        var settings = _siteService.GetSettings<ExternalLoginSettings>();

        options.UseExternalProviderIfOnlyOneDefined = settings.UseExternalProviderIfOnlyOneDefined;
    }
}
