#nullable enable

using Grove.ContentManagement.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Grove.Api;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Nodes;
using Grove;
using Grove.ContentManagement;
using Grove.ContentManagement.Display;
using Grove.DisplayManagement.Components;
using Grove.DisplayManagement.ModelBinding;
using System.Text.Json.Settings;
using Grove.ContentManagement.Records;
using Grove.Contents;
using Grove.Security.Permissions;
using Grove.Access;
using Grove.Data.Scoping;
using YesSql;
using ISession = YesSql.ISession;
using YesSql.Services;
using ContentItem = Grove.Contents.Api.ContentItem;

namespace Grove.Contents.Api;

public sealed class ContentItemsApi : ApiContributor
{
    public override string Name => "ContentItems";

    public override string FeatureId => "Grove.Contents";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("content-items");
        routes.MapGet("", (HttpContext __http, [FromQuery] string? contentType = null, [FromQuery] string? group = null, [FromQuery] string? status = null, [FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) => ApiHandlers.For<ContentItemsHandlers>(__http).ListAsync(contentType, group, status, search, page, pageSize));
        routes.MapPost("{contentItemId}/publish", (HttpContext __http, [FromRoute] string contentItemId) => ApiHandlers.For<ContentItemsHandlers>(__http).PublishAsync(contentItemId));
        routes.MapPost("{contentItemId}/unpublish", (HttpContext __http, [FromRoute] string contentItemId) => ApiHandlers.For<ContentItemsHandlers>(__http).UnpublishAsync(contentItemId));
        routes.MapGet("{contentItemId}", (HttpContext __http, [FromRoute] string contentItemId) => ApiHandlers.For<ContentItemsHandlers>(__http).GetAsync(contentItemId));
        routes.MapGet("{contentItemId}/view", (HttpContext __http, [FromRoute] string contentItemId, [FromQuery] string? parts = null) => ApiHandlers.For<ContentItemsHandlers>(__http).ViewAsync(contentItemId, parts));
        routes.MapGet("{contentItemId}/display", (HttpContext __http, [FromRoute] string contentItemId, [FromServices] IAccessDecision decision, [FromServices] IContentItemDisplayManager displayManager, [FromServices] IDisplayTreeBuilder trees, [FromServices] IDisplayTreeFilter filter, [FromServices] IDisplayHtmlRenderer htmlRenderer, [FromServices] IUpdateModelAccessor updaters, [FromQuery] string? displayType = null, [FromQuery] string? format = null) => ApiHandlers.For<ContentItemsHandlers>(__http).DisplayAsync(contentItemId, decision, displayManager, trees, filter, htmlRenderer, updaters, displayType, format));
        routes.MapPost("", (HttpContext __http, [FromBody] ContentItemWriteRequest request) => ApiHandlers.For<ContentItemsHandlers>(__http).CreateAsync(request));
        routes.MapPut("{contentItemId}", (HttpContext __http, [FromRoute] string contentItemId, [FromBody] ContentItemWriteRequest request) => ApiHandlers.For<ContentItemsHandlers>(__http).UpdateAsync(contentItemId, request));
        routes.MapDelete("{contentItemId}", (HttpContext __http, [FromRoute] string contentItemId) => ApiHandlers.For<ContentItemsHandlers>(__http).DeleteAsync(contentItemId));
        routes.MapPost("{contentItemId}/clone", (HttpContext __http, [FromRoute] string contentItemId) => ApiHandlers.For<ContentItemsHandlers>(__http).CloneAsync(contentItemId));
        routes.MapPut("{contentItemId}/draft", (HttpContext __http, [FromRoute] string contentItemId, [FromBody] ContentItemWriteRequest request) => ApiHandlers.For<ContentItemsHandlers>(__http).SaveDraftAsync(contentItemId, request));
        routes.MapPost("{contentItemId}/discard-draft", (HttpContext __http, [FromRoute] string contentItemId) => ApiHandlers.For<ContentItemsHandlers>(__http).DiscardDraftAsync(contentItemId));
        routes.MapGet("{contentItemId}/versions", (HttpContext __http, [FromRoute] string contentItemId) => ApiHandlers.For<ContentItemsHandlers>(__http).VersionsAsync(contentItemId));
        routes.MapGet("by-handle/{handle}", (HttpContext __http, [FromRoute] string handle) => ApiHandlers.For<ContentItemsHandlers>(__http).GetByHandle(handle));
    }
}

public sealed class ContentItemsHandlers(
    IPlatformHelper platformHelper,
    ISession session,
    IContentManager contentManager,
    Grove.ContentTypes.IFieldVisibilityEnforcer visibilityEnforcer,
    Grove.ContentGroups.ContentGroupService contentGroups,
    IAuthorizationService authorizationService,
    ICallerContextAccessor callerAccessor,
    IScopeSetProvider scopes) : ApiHandlers
{
    private static readonly string[] DefaultViewFields =
    [
        nameof(Grove.ContentManagement.ContentItem.DisplayText),
        nameof(Grove.ContentManagement.ContentItem.ContentType),
    ];

    public async Task<IResult> ListAsync(
        [FromQuery] string? contentType = null,
        [FromQuery] string? group = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        // No up-front permission check (docs/access.md § One decision, ruled 2026-10-13): the
        // route gate admits the page, and the query below carries the caller's view scope as a
        // server-side predicate, so a holder of only ListContent_{type} gets that one type.
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        // The caller's content scope, from the one scope set: a list never returns a row the
        // caller may not see, whatever else it is filtered by.
        var caller = callerAccessor.Current ?? throw new InvalidOperationException("No caller is set for this request.");
        var scope = (await scopes.GetAsync(caller, HttpContext.RequestAborted)).Require(nameof(ContentItemIndex));
        var query = session.Query<Grove.ContentManagement.ContentItem, ContentItemIndex>()
            .With<ContentItemIndex>(index => index.Latest)
            .Where(ScopeExpressions.ToPredicate<ContentItemIndex>(scope));

        if (!string.IsNullOrWhiteSpace(contentType))
        {
            query = query.With<ContentItemIndex>(index => index.ContentType == contentType);
        }

        // A group is a set of content SOURCES (types, option lists, ...), so it expands
        // to "type in {..} OR item id in {..}". An unknown group is a 404, not an
        // unfiltered list; a known group with nothing resolvable yields no rows.
        if (!string.IsNullOrWhiteSpace(group))
        {
            var filter = await contentGroups.ResolveFilterAsync(group, HttpContext.RequestAborted);
            if (filter is null)
            {
                return NotFound();
            }

            if (filter.IsEmpty)
            {
                return Ok(new ContentItemListResult([], 0, page, pageSize));
            }

            var groupTypes = filter.ContentTypes.ToArray();
            var groupIds = filter.ContentItemIds.ToArray();
            // One predicate on the already-joined index: YesSql's Any() emits a second
            // alias for the same index and the SQL breaks; || on one With() does not.
            query = (groupTypes.Length, groupIds.Length) switch
            {
                (> 0, 0) => query.With<ContentItemIndex>(index => index.ContentType.IsIn(groupTypes)),
                (0, > 0) => query.With<ContentItemIndex>(index => index.ContentItemId.IsIn(groupIds)),
                _ => query.With<ContentItemIndex>(index => index.ContentType.IsIn(groupTypes) || index.ContentItemId.IsIn(groupIds)),
            };
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.With<ContentItemIndex>(index => index.DisplayText.Contains(search));
        }

        if (string.Equals(status, "published", StringComparison.OrdinalIgnoreCase))
        {
            query = query.With<ContentItemIndex>(index => index.Published);
        }
        else if (string.Equals(status, "draft", StringComparison.OrdinalIgnoreCase))
        {
            query = query.With<ContentItemIndex>(index => !index.Published);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(index => index.ModifiedUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ListAsync();

        return Ok(new ContentItemListResult(items.Select(ContentItem.From).ToArray(), total, page, pageSize));
    }

    public Task<IResult> PublishAsync(string contentItemId) => ChangePublicationAsync(contentItemId, publish: true);

    public Task<IResult> UnpublishAsync(string contentItemId) => ChangePublicationAsync(contentItemId, publish: false);

    public async Task<IResult> GetAsync(string contentItemId)
    {
        var item = await contentManager.GetAsync(contentItemId, VersionOptions.Latest);
        if (item is null) return NotFound();
        return await authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, item)
            ? Ok(ContentItem.From(item))
            : Forbid();
    }

    // Unlike GetAsync above (full item, gated on EditContent - the content-editor
    // shape Admin's Api.cs already depends on), this is for any caller reading a
    // published item's data for display: anonymous Site visitors, logged-in members,
    // or admins - all through the same action, with the RESPONSE SHAPE driven by
    // what's requested (?parts=) and what that specific caller is permitted to see,
    // not by which endpoint they called. An admin who can view the item gets the same
    // fields back as anyone else who can view it; this isn't a "public" vs "admin"
    // endpoint, it's permission-shaped for whoever's asking.
    //
    // The item is decided once (ViewContent); each requested field is then a part the caller's
    // scope may narrow: a scope provider registered for the type and part
    // (Grove.Access.ContentPartScope, docs/permissions.md ruling 2026-10-10) hides it where its
    // rule does not admit the item; a part with no provider follows the item.
    public async Task<IResult> ViewAsync(
        string contentItemId,
        [FromQuery] string? parts = null)
    {
        var item = await contentManager.GetAsync(contentItemId, VersionOptions.Published);
        if (item is null)
        {
            return NotFound();
        }

        var requestedFields = string.IsNullOrWhiteSpace(parts)
            ? DefaultViewFields
            : parts.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.ViewContent, item))
        {
            return Forbid();
        }

        var caller = callerAccessor.Current;
        var scope = caller is null ? null : await scopes.GetAsync(caller, HttpContext.RequestAborted);
        var result = new Dictionary<string, object?>();
        foreach (var field in requestedFields)
        {
            if (scope is null || !ContentPartScope.Admits(scope, item.ContentType, field, column => IndexColumn(item, column)))
            {
                continue;
            }

            result[field] = field switch
            {
                nameof(Grove.ContentManagement.ContentItem.DisplayText) => item.DisplayText,
                nameof(Grove.ContentManagement.ContentItem.ContentType) => item.ContentType,
                _ => item.Content[field],
            };
        }

        return result.Count == 0 ? Forbid() : Ok(result);
    }

    /// <summary>The item's content index columns, for a part's scope rule.</summary>
    private static object? IndexColumn(Grove.ContentManagement.ContentItem item, string column) => column switch
    {
        nameof(ContentItemIndex.ContentItemId) => item.ContentItemId,
        nameof(ContentItemIndex.ContentItemVersionId) => item.ContentItemVersionId,
        nameof(ContentItemIndex.ContentType) => item.ContentType,
        nameof(ContentItemIndex.Owner) => item.Owner,
        nameof(ContentItemIndex.Author) => item.Author,
        nameof(ContentItemIndex.Published) => item.Published,
        nameof(ContentItemIndex.Latest) => item.Latest,
        nameof(ContentItemIndex.DisplayText) => item.DisplayText,
        nameof(ContentItemIndex.CreatedUtc) => item.CreatedUtc,
        nameof(ContentItemIndex.ModifiedUtc) => item.ModifiedUtc,
        nameof(ContentItemIndex.PublishedUtc) => item.PublishedUtc,
        _ => null,
    };

    // The headless display (docs/blazor-display.md § 8): the item's display tree, the same JSON
    // the Blazor renderer draws. Built by the platform's display drivers and placement, mapped
    // once to the tree, with every component the caller may not see already left out (§ 10).
    // An editor may display the latest draft; anyone else the published version.
    public async Task<IResult> DisplayAsync(
        string contentItemId,
        [FromServices] IAccessDecision decision,
        [FromServices] IContentItemDisplayManager displayManager,
        [FromServices] IDisplayTreeBuilder trees,
        [FromServices] IDisplayTreeFilter filter,
        [FromServices] IDisplayHtmlRenderer htmlRenderer,
        [FromServices] IUpdateModelAccessor updaters,
        [FromQuery] string? displayType = null,
        [FromQuery] string? format = null)
    {
        displayType = string.IsNullOrWhiteSpace(displayType) ? PlatformConstants.DisplayType.Detail : displayType.Trim();
        if (displayType.Length > 64 || !displayType.All(char.IsAsciiLetterOrDigit))
        {
            return BadRequest();
        }

        var cancellationToken = HttpContext?.RequestAborted ?? CancellationToken.None;
        var caller = callerAccessor.Current;
        if (caller is null)
        {
            return Forbid();
        }

        var item = await contentManager.GetAsync(contentItemId, VersionOptions.Published);
        if (item is null)
        {
            var latest = await contentManager.GetAsync(contentItemId, VersionOptions.Latest);
            if (latest is not null && await decision.AllowsAsync(caller, CommonPermissions.EditContent.Name, latest, cancellationToken))
            {
                item = latest;
            }
        }

        if (item is null)
        {
            return NotFound();
        }

        var verdict = await decision.DecideAsync(caller, CommonPermissions.ViewContent.Name, item, cancellationToken);
        if (!verdict.IsAllowed)
        {
            return verdict.Verdict == AccessVerdict.NotFound ? NotFound() : Forbid();
        }

        // Decision 12: a published item's tree carries its composite version as the ETag (the
        // item version, the display type, the viewer's scope signature, and everything the tree
        // depends on), compared before anything is built. A draft is never versioned or cached.
        if (item.Published && HttpContext is { } http)
        {
            var versions = http.RequestServices.GetRequiredService<DisplayTreeVersions>();
            var version = await versions.ComputeAsync(
                http.RequestServices.GetServices<IDisplayTreeVersionPart>(),
                ["content-display", item.ContentItemVersionId, displayType, format?.ToLowerInvariant(), caller.ScopeSignature],
                cancellationToken);
            http.Response.Headers.ETag = DisplayTreeVersions.ETag(version);
            http.Response.Headers.CacheControl = "private, no-cache";
            if (DisplayTreeVersions.Matches(http.Request.Headers.IfNoneMatch, version))
            {
                return StatusCode(Microsoft.AspNetCore.Http.StatusCodes.Status304NotModified);
            }
        }

        var shape = await displayManager.BuildDisplayAsync(item, updaters.ModelUpdater, displayType);
        var tree = await trees.BuildAsync(shape, cancellationToken: cancellationToken);

        // The node filter of § 10 over the whole tree: components the viewer may not see and
        // actions the viewer may not run are left out, templates' nodes included.
        var root = await filter.FilterAsync(tree.Root, caller, cancellationToken);
        if (root is null)
        {
            return NotFound();
        }

        // The HTML path (§ 8): the same tree, rendered by the same renderer to static HTML.
        if (string.Equals(format, "html", StringComparison.OrdinalIgnoreCase))
        {
            var html = await htmlRenderer.RenderAsync(root, cancellationToken);
            return Content(html, "text/html; charset=utf-8");
        }

        return Ok(new DisplayTree { Root = root, DisplayType = tree.DisplayType, ScopeSignature = tree.ScopeSignature });
    }

    // Editors as trees (docs/blazor-display.md rework step 5): the item's editor, laid out by the
    // server from its type's definition and the registry's editor components (or a template for
    // ContentEditor__{Type}). "new-{type}" is the editor of a new item of that type.

    public async Task<IResult> CreateAsync([FromBody] ContentItemWriteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ContentType))
        {
            return Forbid();
        }

        // Decided against the new item, so the per-type variation applies (EditContent_{type}),
        // and publishing takes its own right.
        var item = await contentManager.NewAsync(request.ContentType);
        if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, item) ||
            (request.Publish && !await authorizationService.AuthorizeAsync(User, CommonPermissions.PublishContent, item)))
        {
            return Forbid();
        }

        item.DisplayText = request.DisplayText?.Trim() ?? string.Empty;
        ReplaceContent(item, request.Content);
        // Clear-on-save: a field whose visibility condition is false must not keep a
        // stale value the editor was hiding.
        await visibilityEnforcer.ClearHiddenFieldsAsync(item, HttpContext.RequestAborted);
        bool created;
        try
        {
            created = await contentManager.CreateAsync(item, VersionOptions.Draft);
        }
        catch (ContentSaveRefusedException refused)
        {
            // A save guard (a picker's allowed types, a relationship rule) refused the item.
            return BadRequest(new { errors = refused.Errors });
        }

        if (!created) return Conflict();
        if (request.Publish && !await contentManager.PublishAsync(item)) return Conflict();
        return Ok(ContentItem.From(item));
    }

    public async Task<IResult> UpdateAsync(string contentItemId, [FromBody] ContentItemWriteRequest request)
    {
        var item = await contentManager.GetAsync(contentItemId, VersionOptions.Latest);
        if (item is null) return NotFound();
        if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, item)) return Forbid();
        if (request.Publish && !item.Published && !await authorizationService.AuthorizeAsync(User, CommonPermissions.PublishContent, item)) return Forbid();

        item.DisplayText = request.DisplayText?.Trim() ?? string.Empty;
        ReplaceContent(item, request.Content);
        await visibilityEnforcer.ClearHiddenFieldsAsync(item, HttpContext.RequestAborted);
        try
        {
            await contentManager.UpdateAsync(item);
        }
        catch (ContentSaveRefusedException refused)
        {
            return BadRequest(new { errors = refused.Errors });
        }

        if (request.Publish && !item.Published && !await contentManager.PublishAsync(item)) return Conflict();
        return Ok(ContentItem.From(item));
    }

    public async Task<IResult> DeleteAsync(string contentItemId)
    {
        var item = await contentManager.GetAsync(contentItemId, VersionOptions.Latest);
        if (item is null)
        {
            return NotFound();
        }

        if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.DeleteContent, item))
        {
            return Forbid();
        }

        return await contentManager.RemoveAsync(item) ? NoContent() : Conflict();
    }

    /// <summary>Copies the item into a new draft (CloneContent).</summary>
    public async Task<IResult> CloneAsync(string contentItemId)
    {
        var item = await contentManager.GetAsync(contentItemId, VersionOptions.Latest);
        if (item is null) return NotFound();
        if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.CloneContent, item)) return Forbid();
        try
        {
            return Ok(ContentItem.From(await contentManager.CloneAsync(item)));
        }
        catch (ContentSaveRefusedException refused)
        {
            return BadRequest(new { errors = refused.Errors });
        }
    }

    /// <summary>Saves the editor's state as a draft, leaving the published version as it is.</summary>
    public async Task<IResult> SaveDraftAsync(string contentItemId, [FromBody] ContentItemWriteRequest request)
    {
        var item = await contentManager.GetAsync(contentItemId, VersionOptions.DraftRequired);
        if (item is null) return NotFound();
        if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, item)) return Forbid();

        item.DisplayText = request.DisplayText?.Trim() ?? string.Empty;
        ReplaceContent(item, request.Content);
        await visibilityEnforcer.ClearHiddenFieldsAsync(item, HttpContext.RequestAborted);
        try
        {
            await contentManager.SaveDraftAsync(item);
        }
        catch (ContentSaveRefusedException refused)
        {
            return BadRequest(new { errors = refused.Errors });
        }

        return Ok(ContentItem.From(item));
    }

    /// <summary>Drops the item's draft; the published version becomes the latest again.</summary>
    public async Task<IResult> DiscardDraftAsync(string contentItemId)
    {
        var item = await contentManager.GetAsync(contentItemId, VersionOptions.Latest);
        if (item is null) return NotFound();
        // Discarding a draft deletes a version, as the retired MVC action required.
        if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.DeleteContent, item)) return Forbid();
        if (item.Published) return BadRequest("The item has no draft.");
        if (await contentManager.GetAsync(contentItemId, VersionOptions.Published) is null) return BadRequest("The item was never published; delete it instead.");

        await contentManager.DiscardDraftAsync(item);
        var published = await contentManager.GetAsync(contentItemId, VersionOptions.Published);
        return Ok(ContentItem.From(published!));
    }

    /// <summary>Every stored version of the item, newest first.</summary>
    public async Task<IResult> VersionsAsync(string contentItemId)
    {
        var item = await contentManager.GetAsync(contentItemId, VersionOptions.Latest);
        if (item is null) return NotFound();
        if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.ViewContent, item)) return Forbid();

        var versions = await session.QueryIndex<ContentItemIndex>(index => index.ContentItemId == contentItemId).ListAsync();
        return Ok(versions
            .OrderByDescending(index => index.ModifiedUtc ?? index.CreatedUtc)
            .Select(index => new ContentItemVersion(index.ContentItemVersionId, index.Published, index.Latest, index.DisplayText, index.Author, index.ModifiedUtc, index.PublishedUtc))
            .ToArray());
    }

    public async Task<IResult> GetByHandle(string handle)
    {
        var contentItem = await platformHelper.GetContentItemByHandleAsync(handle);
        if (contentItem is null) return NotFound();
        return await authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, contentItem)
            ? Ok(ContentItem.From(contentItem))
            : Forbid();
    }

    private async Task<IResult> ChangePublicationAsync(string contentItemId, bool publish)
    {
        var item = await contentManager.GetAsync(contentItemId, VersionOptions.Latest);
        if (item is null)
        {
            return NotFound();
        }

        if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.PublishContent, item))
        {
            return Forbid();
        }

        var changed = publish
            ? await contentManager.PublishAsync(item)
            : await contentManager.UnpublishAsync(item);
        return changed ? NoContent() : Conflict();
    }

    // Goes through ContentItem.Merge rather than editing the JSON in place: NewAsync
    // and GetAsync weld part objects into the item's element cache, and a cache that
    // is not reset keeps serving those stale parts to everything that runs on save
    // (index providers, handlers) instead of the content the request wrote.
    private static void ReplaceContent(Grove.ContentManagement.ContentItem item, JsonObject? content)
    {
        item.Content.Clear();
        item.Merge(content?.DeepClone() ?? new JsonObject(), new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace });
    }
}
