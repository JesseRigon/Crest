using System.Security.Claims;
using Crest.Access;
using Crest.Display;
using Crest.DisplayManagement.Components;
using Crest.Templates.Models;
using Crest.Templates.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Api;

/// <summary>
/// The host's display endpoints (docs/server.md § 5): the routes that answer with a display tree
/// the server's builders produce (the site page, the editor trees, the designed page preview).
/// They are the Blazor host's, not a module's API; the records they render are the modules'.
/// </summary>
internal sealed class DisplayApi : ApiContributor
{
    public override string Name => "Display";

    public override string FeatureId => "Crest";

    public override void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("site/page", GetSitePageAsync).AllowAnonymous();
        group.MapPost("pages/{id}/preview", PreviewDesignedPageAsync);
        group.MapGet("custom-settings/{type}/editor", CustomSettingsEditorAsync);
        group.MapGet("content-items/{contentItemId}/editor", ContentItemEditorAsync);
    }

    // The public site's pages (docs/blazor-display.md rework step 3): the site shell's content
    // route asks for the page of a path and renders the tree it gets. Anonymous at the endpoint;
    // the item itself is decided for the caller (ViewContent), and the tree is filtered for them.
    // Decision 12: the page's composite version (everything the tree depends on, the path and
    // the viewer's scope) is its ETag, compared before anything is built; an anonymous viewer's
    // page is shared through the server cache, a signed-in viewer's never is.
    private static async Task<IResult> GetSitePageAsync(
        [FromQuery] string? path,
        HttpContext context,
        [FromServices] SitePageBuilder pages,
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
            return TypedResults.Ok(new SitePageResult(refused.Status, refused.Title, refused.Tree));
        }

        var version = await versions.ComputeAsync(parts, ["site-page", SitePageBuilder.Normalize(path), context.Request.QueryString.Value, caller.ScopeSignature], cancellationToken);
        var shared = caller.UserId is null && !caller.IsSuperUser && !caller.IsSystem;
        context.Response.Headers.ETag = DisplayTreeVersions.ETag(version);
        context.Response.Headers.CacheControl = shared ? "public, no-cache" : "private, no-cache";
        if (DisplayTreeVersions.Matches(context.Request.Headers.IfNoneMatch, version))
        {
            return TypedResults.StatusCode(StatusCodes.Status304NotModified);
        }

        if (shared && cache.TryGet<SitePageResult>(version, out var cached) && cached is not null)
        {
            return TypedResults.Ok(cached);
        }

        var page = await pages.BuildAsync(path, cancellationToken);
        var result = new SitePageResult(page.Status, page.Title, page.Tree);
        if (shared)
        {
            cache.Set(version, result, System.Text.Json.JsonSerializer.Serialize(result).Length);
        }

        return TypedResults.Ok(result);
    }

    // Editors as trees (docs/blazor-display.md rework step 5): the item's editor, laid out by the
    // server from its type's definition and the registry's editor components (or a template for
    // ContentEditor__{Type}). "new-{type}" is the editor of a new item of that type.
    private static async Task<IResult> ContentItemEditorAsync(
        [FromRoute] string contentItemId,
        [FromServices] ContentEditorTreeBuilder editors,
        [FromServices] Crest.ContentManagement.IContentManager contentManager,
        [FromServices] Crest.Access.ICallerContextAccessor callers,
        [FromServices] Crest.Access.IAccessDecision decision,
        CancellationToken cancellationToken)
    {
        var caller = callers.Current;
        if (caller is null)
        {
            return TypedResults.Forbid();
        }

        Crest.ContentManagement.ContentItem? item;
        if (contentItemId.StartsWith("new-", StringComparison.Ordinal))
        {
            // Decided as the caller's own new item, as creating it will be (owner-scoped rights apply).
            item = await contentManager.NewAsync(contentItemId["new-".Length..]);
            item.Owner = caller.UserName;
            item.Author = caller.UserName;
        }
        else
        {
            item = await contentManager.GetAsync(contentItemId, Crest.ContentManagement.VersionOptions.Latest);
        }

        if (item is null || !await decision.AllowsAsync(caller, Crest.Contents.CommonPermissions.EditContent.Name, item, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var tree = await editors.BuildAsync(item, cancellationToken);
        return tree is null ? TypedResults.NotFound() : TypedResults.Ok(tree);
    }

    /// <summary>A custom settings type's editor tree, for the custom settings page (Crest.CustomSettings' API saves it).</summary>
    private static async Task<IResult> CustomSettingsEditorAsync(
        [FromRoute] string type,
        HttpContext context,
        [FromServices] ContentEditorTreeBuilder editors,
        [FromServices] Crest.Settings.ISiteService siteService,
        CancellationToken cancellationToken)
    {
        if (context.RequestServices.GetService<Crest.CustomSettings.Services.CustomSettingsService>() is not { } service
            || await service.GetSettingsTypeAsync(type) is not { } definition)
        {
            return TypedResults.NotFound();
        }

        if (!await service.CanUserCreateSettingsAsync(definition))
        {
            return TypedResults.Forbid();
        }

        var tree = await editors.BuildAsync(await service.GetSettingsAsync(await siteService.GetSiteSettingsAsync(), definition), cancellationToken);
        return tree is null ? TypedResults.NotFound() : TypedResults.Ok(tree);
    }

    /// <summary>
    /// A designed page's draft rendered as the site would render it at <paramref name="path"/> (its
    /// route's values read from it), with the viewer's rights: Design to see the draft, and the
    /// page's Data permission through the page itself. Never cached.
    /// </summary>
    private static async Task<IResult> PreviewDesignedPageAsync(
        [FromRoute] string id,
        [FromQuery] string? path,
        HttpContext context,
        ClaimsPrincipal user,
        [FromServices] DesignedPageStore store,
        [FromServices] IAuthorizationService authorization,
        [FromServices] TemplatePreview preview,
        [FromServices] SitePageBuilder pages,
        CancellationToken cancellationToken)
    {
        if (await store.GetAsync(id) is not { } page || !await authorization.AuthorizeAsync(user, DesignedPagePermissions.Design(page.Name)))
        {
            return TypedResults.NotFound();
        }

        if (page.Bucket != DesignedPagesConstants.SiteBucket)
        {
            return TypedResults.BadRequest(new Crest.Templates.Api.DesignedPageValidation(false, [new(page.Name, "bucket", "Only site pages render yet.")]));
        }

        IReadOnlyDictionary<string, string> values = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(path) && !DesignedPageRoutes.TryMatch(page.Route, path, out values))
        {
            return TypedResults.BadRequest(new Crest.Templates.Api.DesignedPageValidation(false, [new(page.Name, "route", $"'{path}' does not match the route '{page.Route}'.")]));
        }

        preview.Name = page.ShapeName;
        preview.Source = page.Source;
        context.Response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(await pages.BuildDesignedAsync(page, values, cancellationToken));
    }
}

/// <summary>A public page: the status it answers with (200, 403, 404), its title and its display tree.</summary>
public sealed record SitePageResult(int Status, string? Title, DisplayTree? Tree);
