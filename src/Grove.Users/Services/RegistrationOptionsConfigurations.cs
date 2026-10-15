using Microsoft.Extensions.Options;
using Grove.Settings;
using Grove.Users.Models;

namespace Grove.Users.Services;

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
    }
}
