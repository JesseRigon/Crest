#nullable enable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Grove.Api;
using Microsoft.Extensions.Options;
using Grove.Contents.Management.Routing;
using Grove.Display.Management.Components;
using Grove.Infrastructure.Settings;

namespace Grove.Infrastructure.Settings.Api;

public sealed class SiteSettingsApi : ApiContributor
{
    public override string Name => "SiteSettings";

    public override string FeatureId => "Grove.Infrastructure.Settings";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("site");
        routes.MapGet("", ApiHandlers.Handle<SiteSettingsHandlers>(h => h.Get()));
        routes.MapGet("home", ApiHandlers.Handle<SiteSettingsHandlers>(h => h.GetHomeAsync())).AllowAnonymous();
        routes.MapGet("branding", (HttpContext __http, [FromServices] SiteIdentity identity) => ApiHandlers.For<SiteSettingsHandlers>(__http).GetBrandingAsync(identity)).AllowAnonymous();
        routes.MapPut("", (HttpContext __http, [FromBody] SiteSettingsUpdate update) => ApiHandlers.For<SiteSettingsHandlers>(__http).Put(update));
    }
}

public sealed class SiteSettingsHandlers(
    ISiteService siteService,
    IAuthorizationService authorization,
    IOptions<AutorouteOptions> autorouteOptions) : ApiHandlers
{
    public async Task<IResult> Get()
    {
        if (!await authorization.AuthorizeAsync(User, SettingsPermissions.ManageSettings)) return Forbid();
        var site = await siteService.GetSiteSettingsAsync();
        return Ok(SiteSettingsModel.From(site));
    }

    // Anonymous, permission-shaped like ContentItemsController.ViewAsync: any caller may
    // ask "what is the site's home content item", the same way any caller may load the
    // home page in a browser. ISite.HomeRoute is written by AutoroutePartHandler when a
    // content item's AutoroutePart.SetHomepage is published (Grove.Routes.Auto,
    // now a real Grove.Infrastructure.Server manifest dependency - see Manifest.cs) - reading it here
    // via IOptions<AutorouteOptions>.ContentItemIdKey ("contentItemId", configured in
    // Grove.Contents/Startup.cs) is the same lookup HomeRouteTransformer performs
    // server-side; no invented literal, just the one real key Grove itself defines.
    [AllowAnonymous]
    public async Task<IResult> GetHomeAsync()
    {
        var site = await siteService.GetSiteSettingsAsync();
        var contentItemIdKey = autorouteOptions.Value.ContentItemIdKey;

        if (site.HomeRoute is null
            || !site.HomeRoute.TryGetValue(contentItemIdKey, out var contentItemId)
            || contentItemId is not string { Length: > 0 } homeContentItemId)
        {
            return NotFound();
        }

        return Ok(new SiteHomeResult(homeContentItemId));
    }

    // Anonymous: the login page greets with the tenant's own name, before anyone is signed
    // in. The branding display name IS ISite.SiteName (docs/branding.md) - the same value the
    // General settings screen edits - and nothing else from ISite is exposed here.
    // Interim: the client reads the `site` query (api/grove/queries/site/run) and falls back here only
    // for a tenant whose Anonymous role lacks the query's permission. Answered from the same
    // query in process, so the two never disagree; it goes with the hand-built login page.
    [AllowAnonymous]
    public async Task<IResult> GetBrandingAsync([FromServices] SiteIdentity identity)
    {
        var result = await identity.GetAsync(HttpContext.RequestAborted);
        return Ok(new SiteBrandingResult(result.Name, result.Branding));
    }

    public async Task<IResult> Put(SiteSettingsUpdate update)
    {
        if (!await authorization.AuthorizeAsync(User, SettingsPermissions.ManageSettings)) return Forbid();
        var site = await siteService.LoadSiteSettingsAsync();

        site.SiteName = update.SiteName?.Trim() ?? string.Empty;
        site.PageTitleFormat = update.PageTitleFormat?.Trim() ?? string.Empty;
        site.BaseUrl = update.BaseUrl?.Trim() ?? string.Empty;
        site.TimeZoneId = update.TimeZoneId?.Trim() ?? string.Empty;
        site.Calendar = update.Calendar?.Trim() ?? string.Empty;
        site.PageSize = update.PageSize;
        site.MaxPageSize = update.MaxPageSize;
        site.MaxPagedCount = update.MaxPagedCount;
        site.AppendVersion = update.AppendVersion;
        site.UseCdn = update.UseCdn;
        site.CdnBaseUrl = update.CdnBaseUrl?.Trim() ?? string.Empty;
        site.ResourceDebugMode = ParseEnum(update.ResourceDebugMode, site.ResourceDebugMode);
        site.CacheMode = ParseEnum(update.CacheMode, site.CacheMode);

        await siteService.UpdateSiteSettingsAsync(site);

        return Ok(SiteSettingsModel.From(site));
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var result) ? result : fallback;
}
