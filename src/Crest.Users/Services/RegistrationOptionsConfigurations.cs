using Microsoft.Extensions.Options;
using Crest.Settings;
using Crest.Users.Models;

namespace Crest.Users.Services;

public sealed class RegistrationOptionsConfigurations : IConfigureOptions<RegistrationOptions>
{
    private readonly ISiteService _siteService;

    public RegistrationOptionsConfigurations(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public void Configure(RegistrationOptions options)
    {
        var settings = _siteService.GetSettings<RegistrationSettings>();

        options.UsersMustValidateEmail = settings.UsersMustValidateEmail;
        options.UsersAreModerated = settings.UsersAreModerated;
        options.UseSiteTheme = settings.UseSiteTheme;
    }
}
