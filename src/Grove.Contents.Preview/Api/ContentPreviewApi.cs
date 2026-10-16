#nullable enable

using System.Text.Json.Nodes;
using System.Text.Json.Settings;
using Grove.Access;
using Grove.Api;
using Grove.Contents.Management;
using Grove.Contents.Management.Display;
using Grove.Contents.Management.Handlers;
using Grove.Contents;
using Grove.Display.Management.Components;
using Grove.Modules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Contents.Preview.Api;

/// <summary>
/// The preview of an item as it is being edited (<c>POST api/grove/content-preview</c>): the
/// unsaved content is rendered the way the item's display renders (its display drivers,
/// placement and templates, mapped to the display tree, filtered for the caller) and answered as
/// HTML, or as the display tree with <c>?format=tree</c>. Nothing is stored: the item exists for
/// the request only, under the edited item's id so that what reads the item by id (its route,
/// its relationships) sees the preview. The item's before hooks (creating or updating, then
/// publishing) run first, so the preview shows the values its flows would write (its display text,
/// its URL); their refusals do not stop the preview, and the request's unit of work is rolled back
/// (docs/content-engine.md › Behaviour is workflows). The native editor's preview pane calls it.
/// </summary>
public sealed class ContentPreviewApi : ApiContributor
{
    public override string Name => "ContentPreview";

    public override string FeatureId => "Grove.Contents.Preview";

    public override void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("content-preview", (HttpContext __http, [FromBody] ContentPreviewRequest request, [FromQuery] string? format = null)
            => ApiHandlers.For<ContentPreviewHandlers>(__http).PreviewAsync(request, format));
    }
}

/// <summary>The edited item: its type, its id when it exists, its display text and its content as the editor holds it.</summary>
public sealed record ContentPreviewRequest(string? ContentType, string? ContentItemId, string? DisplayText, JsonObject? Content, string? DisplayType);

public sealed class ContentPreviewHandlers(
    IContentManager contentManager,
    IContentManagerSession contentManagerSession,
    IContentLifecycle lifecycle,
    Grove.Data.Documents.IDocumentStore documentStore,
    IContentItemDisplayManager displayManager,
    IDisplayTreeBuilder trees,
    IDisplayTreeFilter filter,
    IDisplayHtmlRenderer htmlRenderer,
    IAuthorizationService authorizationService,
    ICallerContextAccessor callers,
    IClock clock) : ApiHandlers
{
    public async Task<IResult> PreviewAsync([FromBody] ContentPreviewRequest request, string? format)
    {
        if (string.IsNullOrWhiteSpace(request.ContentType))
        {
            return ApiProblems.Validation(nameof(ContentPreviewRequest.ContentType), "The content type is required.");
        }

        var displayType = string.IsNullOrWhiteSpace(request.DisplayType) ? PlatformConstants.DisplayType.Detail : request.DisplayType.Trim();
        if (displayType.Length > 64 || !displayType.All(char.IsAsciiLetterOrDigit))
        {
            return ApiProblems.Validation(nameof(ContentPreviewRequest.DisplayType), "The display type is not valid.");
        }

        var item = await contentManager.NewAsync(request.ContentType);
        ContentItem? stored = null;
        if (!string.IsNullOrWhiteSpace(request.ContentItemId))
        {
            // An existing item is previewed under its own id, decided as itself.
            stored = await contentManager.GetAsync(request.ContentItemId, VersionOptions.Latest);
            if (stored is null || stored.ContentType != request.ContentType)
            {
                return NotFound();
            }

            item.ContentItemId = stored.ContentItemId;
            item.ContentItemVersionId = stored.ContentItemVersionId;
            item.Owner = stored.Owner;
            item.Author = stored.Author;
            item.CreatedUtc = stored.CreatedUtc;
            if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, stored))
            {
                return Forbid();
            }
        }
        else
        {
            item.Owner = User.Identity?.Name;
            item.Author = User.Identity?.Name;
            if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, item))
            {
                return Forbid();
            }
        }

        if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.PreviewContent, item))
        {
            return Forbid();
        }

        // A request-only item: an id no stored document has, the edited content, shown as published.
        item.Id = -1;
        item.DisplayText = request.DisplayText?.Trim() ?? string.Empty;
        item.Content.Clear();
        item.Merge(request.Content?.DeepClone() ?? new JsonObject(), new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace });
        item.CreatedUtc ??= clock.UtcNow;
        item.ModifiedUtc = clock.UtcNow;
        item.PublishedUtc = clock.UtcNow;
        item.Published = true;
        item.Latest = true;

        // Drivers and handlers can tell a preview (ContentPreviewFeature), and what loads the item
        // by id in this request gets the preview.
        HttpContext.Features.Set(ContentPreviewFeature.Instance);
        contentManagerSession.Store(item);

        try
        {
            return await RenderAsync(item, stored, displayType, format);
        }
        finally
        {
            // Nothing the preview's hooks wrote commits, and nothing they queued for after the
            // commit (cache evictions, route entries) runs.
            await documentStore.CancelAsync();
        }
    }

    private async Task<IResult> RenderAsync(ContentItem item, ContentItem? stored, string displayType, string? format)
    {
        // The before hooks write what the item would hold once saved and published; a refusal
        // leaves the preview as edited.
        var previous = stored is null ? ContentPreviousState.None : ContentPreviousState.Copy(stored);
        await lifecycle.CheckAsync(stored is null ? ContentLifecyclePoints.Creating : ContentLifecyclePoints.Updating, item, previous);
        await lifecycle.CheckAsync(ContentLifecyclePoints.Publishing, item, previous);

        var cancellationToken = HttpContext.RequestAborted;
        var shape = await displayManager.BuildDisplayAsync(item, displayType);
        var tree = await trees.BuildAsync(shape, cancellationToken: cancellationToken);
        var root = callers.Current is { } caller ? await filter.FilterAsync(tree.Root, caller, cancellationToken) : null;
        if (root is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "no-store";
        if (string.Equals(format, "tree", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new DisplayTree { Root = root, DisplayType = tree.DisplayType, ScopeSignature = tree.ScopeSignature });
        }

        return TypedResults.Content(await htmlRenderer.RenderAsync(root, cancellationToken), "text/html; charset=utf-8");
    }
}
