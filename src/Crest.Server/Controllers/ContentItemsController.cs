using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;
using Crest;
using Crest.ContentManagement;
using Crest.ContentManagement.Display;
using Crest.DisplayManagement.Components;
using Crest.DisplayManagement.ModelBinding;
using System.Text.Json.Settings;
using Crest.ContentManagement.Records;
using Crest.Contents;
using Crest.Security.Permissions;
using Crest.Access;
using Crest.Data.Scoping;
using YesSql;
using YesSql.Services;
using Crest.ViewModels;
using ContentItem = Crest.ViewModels.ContentItem;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/content-items")]
public sealed class ContentItemsController(
    IPlatformHelper platformHelper,
    ISession session,
    IContentManager contentManager,
    Crest.Services.CrestFieldVisibilityEnforcer visibilityEnforcer,
    Crest.ContentGroups.CrestContentGroupService contentGroups,
    IAuthorizationService authorizationService,
    ICallerContextAccessor callerAccessor,
    IScopeSetProvider scopes) : ControllerBase
{
    private static readonly string[] DefaultViewFields =
    [
        nameof(Crest.ContentManagement.ContentItem.DisplayText),
        nameof(Crest.ContentManagement.ContentItem.ContentType),
    ];

    [HttpGet]
    public async Task<ActionResult<ContentItemListResult>> ListAsync(
        [FromQuery] string? contentType = null,
        [FromQuery] string? group = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        // A type-filtered list authorizes against a stand-in item of that type
        // (stock's own AuthorizeContentTypeDefinitionsAsync pattern), so Securable
        // types enforce their dynamic ListContent_{Type} permission through the
        // standard handler chain; the unfiltered list keeps the global check.
        if (string.IsNullOrWhiteSpace(contentType))
        {
            if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.ListContent))
            {
                return Forbid();
            }
        }
        else if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.ListContent, await contentManager.NewAsync(contentType)))
        {
            return Forbid();
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        // The caller's content scope, from the one scope set: a list never returns a row the
        // caller may not see, whatever else it is filtered by.
        var caller = callerAccessor.Current ?? throw new InvalidOperationException("No caller is set for this request.");
        var scope = (await scopes.GetAsync(caller, HttpContext.RequestAborted)).Require(nameof(ContentItemIndex));
        var query = session.Query<Crest.ContentManagement.ContentItem, ContentItemIndex>()
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

    [HttpPost("{contentItemId}/publish")]
    public Task<IActionResult> PublishAsync(string contentItemId) => ChangePublicationAsync(contentItemId, publish: true);

    [HttpPost("{contentItemId}/unpublish")]
    public Task<IActionResult> UnpublishAsync(string contentItemId) => ChangePublicationAsync(contentItemId, publish: false);

    [HttpGet("{contentItemId}")]
    public async Task<ActionResult<ContentItem>> GetAsync(string contentItemId)
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
    // Today every requested field checks the same item-level CommonPermissions.ViewContent
    // - Crest has no built-in per-part permission (see docs/permissions.md for
    // why, and what real per-part permissions would require). That means "permitted"
    // currently means "can view this item at all," not yet differentiated per part -
    // each field's check is already its own step below so a real per-part permission
    // can replace CommonPermissions.ViewContent there later without restructuring this
    // action.
    [HttpGet("{contentItemId}/view")]
    public async Task<ActionResult<Dictionary<string, object?>>> ViewAsync(
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

        var result = new Dictionary<string, object?>();
        foreach (var field in requestedFields)
        {
            if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.ViewContent, item))
            {
                continue;
            }

            result[field] = field switch
            {
                nameof(Crest.ContentManagement.ContentItem.DisplayText) => item.DisplayText,
                nameof(Crest.ContentManagement.ContentItem.ContentType) => item.ContentType,
                _ => item.Content[field],
            };
        }

        return result.Count == 0 ? Forbid() : Ok(result);
    }

    // The headless display (docs/blazor-display.md § 8): the item's display tree, the same JSON
    // the Blazor renderer draws. Built by the platform's display drivers and placement, mapped
    // once to the tree, with every component the caller may not see already left out (§ 10).
    // An editor may display the latest draft; anyone else the published version.
    [HttpGet("{contentItemId}/display")]
    public async Task<ActionResult<DisplayTree>> DisplayAsync(
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

        // TODO(blazor-display step 6): cache the tree per (item.ContentItemVersionId, displayType,
        // caller.ScopeSignature), invalidated on theme, placement, feature and template changes as
        // the shape table is. Never across scope signatures: the tree is built with the viewer's
        // permissions (security audit item 9).
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
    [HttpGet("{contentItemId}/editor")]
    public async Task<ActionResult<DisplayTree>> EditorAsync(
        string contentItemId,
        [FromServices] Crest.Display.ContentEditorTreeBuilder editors,
        [FromServices] IAccessDecision decision,
        CancellationToken cancellationToken)
    {
        var caller = callerAccessor.Current;
        if (caller is null)
        {
            return Forbid();
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
            item = await contentManager.GetAsync(contentItemId, VersionOptions.Latest);
        }

        if (item is null || !await decision.AllowsAsync(caller, CommonPermissions.EditContent.Name, item, cancellationToken))
        {
            return NotFound();
        }

        var tree = await editors.BuildAsync(item, cancellationToken);
        return tree is null ? NotFound() : Ok(tree);
    }

    [HttpPost]
    public async Task<ActionResult<ContentItem>> CreateAsync([FromBody] ContentItemWriteRequest request)
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
        var created = await contentManager.CreateAsync(item, VersionOptions.Draft);
        if (!created) return Conflict();
        if (request.Publish && !await contentManager.PublishAsync(item)) return Conflict();
        return Ok(ContentItem.From(item));
    }

    [HttpPut("{contentItemId}")]
    public async Task<ActionResult<ContentItem>> UpdateAsync(string contentItemId, [FromBody] ContentItemWriteRequest request)
    {
        var item = await contentManager.GetAsync(contentItemId, VersionOptions.Latest);
        if (item is null) return NotFound();
        if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, item)) return Forbid();
        if (request.Publish && !item.Published && !await authorizationService.AuthorizeAsync(User, CommonPermissions.PublishContent, item)) return Forbid();

        item.DisplayText = request.DisplayText?.Trim() ?? string.Empty;
        ReplaceContent(item, request.Content);
        await visibilityEnforcer.ClearHiddenFieldsAsync(item, HttpContext.RequestAborted);
        await contentManager.UpdateAsync(item);
        if (request.Publish && !item.Published && !await contentManager.PublishAsync(item)) return Conflict();
        return Ok(ContentItem.From(item));
    }

    [HttpDelete("{contentItemId}")]
    public async Task<IActionResult> DeleteAsync(string contentItemId)
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

    [HttpGet("by-handle/{handle}")]
    public async Task<ActionResult<ContentItem>> GetByHandle(string handle)
    {
        var contentItem = await platformHelper.GetContentItemByHandleAsync(handle);
        if (contentItem is null) return NotFound();
        return await authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, contentItem)
            ? Ok(ContentItem.From(contentItem))
            : Forbid();
    }

    private async Task<IActionResult> ChangePublicationAsync(string contentItemId, bool publish)
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
    private static void ReplaceContent(Crest.ContentManagement.ContentItem item, JsonObject? content)
    {
        item.Content.Clear();
        item.Merge(content?.DeepClone() ?? new JsonObject(), new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace });
    }
}
