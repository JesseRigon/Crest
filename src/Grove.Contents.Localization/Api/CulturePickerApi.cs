#nullable enable

using System.Globalization;
using Grove.Api;
using Grove.Data.Localization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Routing;

namespace Grove.Contents.Localization.Api;

/// <summary>
/// The site's cultures for the <c>CulturePicker</c> component (<c>api/grove/content-localization/cultures</c>,
/// anonymous): each culture with its native name, and which one the request runs in. Choosing one
/// goes through <c>RedirectToLocalizedContent</c>, which finds the current item's translation in
/// that culture (or the home page's, as the culture picker settings say).
/// </summary>
public sealed class CulturePickerApi : ApiContributor
{
    public override string Name => "CulturePicker";

    public override string FeatureId => "Grove.Contents.Localization.ContentCulturePicker";

    public override void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("content-localization/cultures", ApiHandlers.Handle<CulturePickerHandlers>(h => h.GetAsync())).AllowAnonymous();
    }
}

/// <summary>A site culture as the picker shows it.</summary>
public sealed record SiteCulture(string Name, string NativeName, bool Current);

public sealed class CulturePickerHandlers(ILocalizationService localizationService) : ApiHandlers
{
    public async Task<IResult> GetAsync()
    {
        var current = HttpContext.Features.Get<IRequestCultureFeature>()?.RequestCulture?.UICulture ?? CultureInfo.CurrentUICulture;
        var cultures = (await localizationService.GetSupportedCulturesAsync())
            .Select(name => CultureInfo.GetCultureInfo(name))
            .Select(culture => new SiteCulture(culture.Name, culture.NativeName, string.Equals(culture.Name, current.Name, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        return Ok(cultures);
    }
}
