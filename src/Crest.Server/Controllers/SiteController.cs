using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Crest.ContentManagement.Routing;
using Crest.DisplayManagement.Components;
using Microsoft.AspNetCore.Http;
using Crest.Settings;
using Crest.ViewModels;
using SiteSettings = Crest.ViewModels.SiteSettings;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/site")]
public sealed class SiteController(
    ISiteService siteService,
    IAuthorizationService authorization,
    IOptions<AutorouteOptions> autorouteOptions) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SiteSettings>> Get()
    {
        if (!await authorization.AuthorizeAsync(User, SettingsPermissions.ManageSettings)) return Forbid();
        var site = await siteService.GetSiteSettingsAsync();
        return Ok(SiteSettings.From(site));
    }

    // Anonymous, permission-shaped like ContentItemsController.ViewAsync: any caller may
    // ask "what is the site's home content item", the same way any caller may load the
    // home page in a browser. ISite.HomeRoute is written by AutoroutePartHandler when a
    // content item's AutoroutePart.SetHomepage is published (Crest.Autoroute,
    // now a real Crest.Server manifest dependency - see Manifest.cs) - reading it here
    // via IOptions<AutorouteOptions>.ContentItemIdKey ("contentItemId", configured in
    // Crest.Contents/Startup.cs) is the same lookup HomeRouteTransformer performs
    // server-side; no invented literal, just the one real key Crest itself defines.
    [HttpGet("home")]
    [AllowAnonymous]
    public async Task<ActionResult<SiteHomeResult>> GetHomeAsync()
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

    // The public site's pages (docs/blazor-display.md rework step 3): the site shell's content
    // route asks for the page of a path and renders the tree it gets. Anonymous at the endpoint;
    // the item itself is decided for the caller (ViewContent), and the tree is filtered for them.
    // Decision 12: the page's composite version (everything the tree depends on, the path and
    // the viewer's scope) is its ETag, compared before anything is built; an anonymous viewer's
    // page is shared through the server cache, a signed-in viewer's never is.
    [HttpGet("page")]
    [AllowAnonymous]
    public async Task<ActionResult<SitePageResult>> GetPageAsync(
        [FromQuery] string? path,
        [FromServices] Crest.Display.SitePageBuilder pages,
        [FromServices] Crest.Access.ICallerContextAccessor callers,
        [FromServices] DisplayTreeVersions versions,
        [FromServices] IEnumerable<IDisplayTreeVersionPart> parts,
        [FromServices] DisplayTreeCache cache,
        CancellationToken cancellationToken)
    {
        var caller = callers.Current;
        if (caller is null)
        {
            var refused = await pages.BuildAsync(path, cancellationToken);
            return Ok(new SitePageResult(refused.Status, refused.Title, refused.Tree));
        }

        var version = await versions.ComputeAsync(parts, ["site-page", Crest.Display.SitePageBuilder.Normalize(path), Request.QueryString.Value, caller.ScopeSignature], cancellationToken);
        var shared = caller.UserId is null && !caller.IsSuperUser && !caller.IsSystem;
        Response.Headers.ETag = DisplayTreeVersions.ETag(version);
        Response.Headers.CacheControl = shared ? "public, no-cache" : "private, no-cache";
        if (DisplayTreeVersions.Matches(Request.Headers.IfNoneMatch, version))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        if (shared && cache.TryGet<SitePageResult>(version, out var cached) && cached is not null)
        {
            return Ok(cached);
        }

        var page = await pages.BuildAsync(path, cancellationToken);
        var result = new SitePageResult(page.Status, page.Title, page.Tree);
        if (shared)
        {
            cache.Set(version, result, System.Text.Json.JsonSerializer.Serialize(result).Length);
        }

        return Ok(result);
    }

    // Anonymous: the login page greets with the tenant's own name, before anyone is signed
    // in. The branding display name IS ISite.SiteName (docs/branding.md) - the same value the
    // General settings screen edits - and nothing else from ISite is exposed here.
    // Interim: the client reads the `site` query (api/queries/site) and falls back here only
    // for a tenant whose Anonymous role lacks the query's permission. Answered from the same
    // query in process, so the two never disagree; it goes with the hand-built login page.
    [HttpGet("branding")]
    [AllowAnonymous]
    public async Task<ActionResult<SiteBrandingResult>> GetBrandingAsync([FromServices] global::Crest.Services.SiteIdentity identity)
    {
        var result = await identity.GetAsync(HttpContext.RequestAborted);
        return Ok(new SiteBrandingResult(result.Name, result.Branding));
    }

    [HttpPut]
    public async Task<ActionResult<SiteSettings>> Put(SiteSettingsUpdate update)
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

        return Ok(SiteSettings.From(site));
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var result) ? result : fallback;
}
