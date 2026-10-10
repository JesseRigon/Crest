using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using Crest.Environment.Shell;

namespace Crest.Localization;

/// <summary>
/// The one owner of the request culture providers. The list is assigned, not inserted into:
/// <c>IConfigureOptions&lt;T&gt;</c> ordering across independent registrations is not
/// guaranteed, so two modules each inserting at index 0 would race for the front slot and
/// leave every provider behind them at an unstable position. The tenant-wide culture cookie
/// comes first, then <c>Accept-Language</c> for a visitor that has not resolved a culture yet.
/// </summary>
internal sealed class RequestLocalizationOptionsConfigurations : IConfigureOptions<RequestLocalizationOptions>
{
    private readonly ShellSettings _shellSettings;

    public RequestLocalizationOptionsConfigurations(ShellSettings shellSettings)
    {
        _shellSettings = shellSettings;
    }

    public void Configure(RequestLocalizationOptions options)
    {
        options.RequestCultureProviders =
        [
            new CookieRequestCultureProvider { CookieName = CultureCookie.MakeCookieName(_shellSettings) },
            new AcceptLanguageHeaderRequestCultureProvider(),
        ];
    }
}
