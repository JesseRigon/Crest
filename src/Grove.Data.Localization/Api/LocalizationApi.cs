#nullable enable

using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Grove.Api;
using Microsoft.Extensions.DependencyInjection;
using Grove.Environment.Shell;
using Grove.Entities;
using Grove.Data.Localization.Models;
using Grove.Data.Localization.Services;
using Grove.Infrastructure.Settings;

namespace Grove.Data.Localization.Api;

public sealed class LocalizationApi : ApiContributor
{
    public override string Name => "Localization";

    public override string FeatureId => "Grove.Data.Localization";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("localization");
        routes.MapGet("strings", (HttpContext __http, [FromQuery] string culture) => ApiHandlers.For<LocalizationHandlers>(__http).GetStringsAsync(culture));
        routes.MapGet("", ApiHandlers.Handle<LocalizationHandlers>(h => h.GetAsync()));
        routes.MapPut("", (HttpContext __http, [FromBody] LocalizationModel request) => ApiHandlers.For<LocalizationHandlers>(__http).SaveAsync(request));
    }
}

public sealed class LocalizationHandlers(
    ISiteService sites,
    IShellReleaseManager releases,
    IAuthorizationService authorization,
    ILocalizationService localizationService,
    ILocalizationManager localizationManager,
    IServiceProvider serviceProvider) : ApiHandlers
{
    // Grove.Themes.Admin (Blazor WASM) has no server-side round trip per render, so it cannot
    // use IStringLocalizer<T> directly the way server-rendered Razor/CRM.AdminMenu.cs
    // does. Baking .resx satellite assemblies into the WASM bundle (as
    // Grove.Display.Components already does for its own component-library strings)
    // was explicitly rejected for .Admin's own UI strings: those need to be editable the
    // same way any other translatable content is - via Grove's normal localization
    // tooling (.po files) - without a recompile/redeploy. So instead: this endpoint
    // exposes a resolved per-culture dictionary and the client (GroveApiLocalizer)
    // fetches and caches it, plugging into the same Localizer/ILocalizer seam
    // Grove.Display.Components already defines for exactly this kind of override.
    //
    // Client string keys are INVARIANT LITERALS (T["Some text"], native Grove style),
    // so the same literal a Grove page uses is also the msgid every shipped module
    // catalog uses for that string. The dictionary is therefore layered per key,
    // mirroring the menu caption chain (docs/localization.mmd): stored edit (tenant
    // translation store, this context) -> PO (an entry under this context first, then
    // the most common translation of the literal across ALL shipped catalogs, so
    // upstream's own translations cover Grove pages for free) -> miss, where the client
    // renders the literal itself. Each layer resolves es-ES before es.
    public const string ClientStringsContext = "Grove.Themes.Admin.Client";

    private static readonly string[] ClientContextPreference = [ClientStringsContext];

    public async Task<IResult> GetStringsAsync(string culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return BadRequest("Culture is required.");
        }

        CultureInfo cultureInfo;
        try
        {
            cultureInfo = CultureInfo.GetCultureInfo(culture);
        }
        catch (CultureNotFoundException)
        {
            return BadRequest("Culture must be a valid .NET/BCP 47 culture name.");
        }

        var strings = new Dictionary<string, string>(StringComparer.Ordinal);

        // PO layer: every literal any shipped catalog translates for this culture chain,
        // resolved with the client context preferred over the flat most-common pool.
        // BuildIndex already skips empty and identity entries, so untranslated msgids
        // never mask the client-side literal fallback.
        var chainIndexes = PoTranslationLookup.BuildCultureChainIndexes(localizationManager, cultureInfo);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var index in chainIndexes)
        {
            keys.UnionWith(index.Keys);
        }

        foreach (var key in keys)
        {
            if (PoTranslationLookup.Resolve(chainIndexes, key, null, ClientContextPreference) is { } value)
            {
                strings[key] = value;
            }
        }

        // Store layer on top (stored edit -> PO -> literal; delete walks down): entries
        // the Translations editor holds under the client context, parent culture first so
        // a region-specific edit still wins. Storing the literal itself as the value pins
        // the literal over a shipped PO translation.
        foreach (var overrides in serviceProvider.GetServices<IClientStringOverrides>())
        {
            foreach (var (key, value) in await overrides.GetAsync(cultureInfo, ClientStringsContext))
            {
                strings[key] = value;
            }
        }

        return Ok(strings);
    }



    public async Task<IResult> GetAsync()
    {
        if (!await authorization.AuthorizeAsync(User, LocalizationPermissions.ManageCultures))
        {
            return Forbid();
        }

        return Ok(await CreateDtoAsync());
    }

    public async Task<IResult> SaveAsync(LocalizationModel request)
    {
        if (!await authorization.AuthorizeAsync(User, LocalizationPermissions.ManageCultures))
        {
            return Forbid();
        }

        string[] cultures;
        try
        {
            cultures = NormalizeCultures(request.SupportedCultures);
        }
        catch (CultureNotFoundException)
        {
            return BadRequest("Each supported culture must be a valid .NET/BCP 47 culture name.");
        }

        if (cultures.Length == 0)
        {
            return BadRequest("At least one supported culture is required.");
        }

        var defaultCulture = NormalizeCultureName(request.DefaultCulture);
        if (!cultures.Contains(defaultCulture, StringComparer.OrdinalIgnoreCase))
        {
            defaultCulture = cultures[0];
        }

        // AdminDefaultCulture (rung 3 of the client resolution chain - see
        // docs/localization.md's "Resolution architecture") is optional and, when
        // set, must still be one of the tenant's supported cultures - it is not a way to
        // force an unsupported culture onto the admin area.
        string? adminDefaultCulture = null;
        if (!string.IsNullOrWhiteSpace(request.AdminDefaultCulture))
        {
            adminDefaultCulture = NormalizeCultureName(request.AdminDefaultCulture);
            if (!cultures.Contains(adminDefaultCulture, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest("Admin default culture must be one of the supported cultures.");
            }
        }

        var site = await sites.LoadSiteSettingsAsync();
        site.Alter<LocalizationSettings>(settings =>
        {
            settings.SupportedCultures = cultures;
            settings.DefaultCulture = defaultCulture;
            settings.FallBackToParentCulture = request.FallBackToParentCulture;
        });
        site.Alter<GroveLocalizationSettings>(settings => settings.AdminDefaultCulture = adminDefaultCulture);
        await sites.UpdateSiteSettingsAsync(site);

        // This is Grove's required lifecycle step: RequestLocalizationOptions are
        // rebuilt from the tenant's LocalizationSettings after the tenant reloads.
        releases.RequestRelease();

        return Ok(await CreateDtoAsync(cultures, defaultCulture, request.FallBackToParentCulture, adminDefaultCulture));
    }

    private async Task<LocalizationModel> CreateDtoAsync(
        string[]? cultures = null,
        string? defaultCulture = null,
        bool? fallBackToParentCulture = null,
        string? adminDefaultCulture = null)
    {
        var settings = await sites.GetSettingsAsync<LocalizationSettings>();
        var groveSettings = await sites.GetSettingsAsync<GroveLocalizationSettings>();
        cultures ??= await localizationService.GetSupportedCulturesAsync();
        defaultCulture ??= await localizationService.GetDefaultCultureAsync();
        fallBackToParentCulture ??= settings.FallBackToParentCulture;
        adminDefaultCulture ??= groveSettings.AdminDefaultCulture;

        return new LocalizationModel(
            defaultCulture,
            cultures,
            fallBackToParentCulture.Value,
            adminDefaultCulture,
            localizationService.GetAllCulturesAndAliases()
                .GroupBy(culture => culture.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(culture => culture.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(Culture.From)
                .ToArray());
    }

    private static string[] NormalizeCultures(IEnumerable<string>? values) => (values ?? [])
        .Where(value => value is not null)
        .Select(NormalizeCultureName)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static string NormalizeCultureName(string? value)
    {
        var name = value?.Trim() ?? string.Empty;
        return string.IsNullOrEmpty(name) ? string.Empty : CultureInfo.GetCultureInfo(name).Name;
    }
}
