using Grove.Api;
using Microsoft.AspNetCore.Http;
using Grove.Access;
using Grove.AuditTrail.Indexes;
using Grove.AuditTrail.Models;
using Grove.Contents.Management;
using Grove.Contents.Management.Display;
using Grove.Contents.AuditTrail.Models;
using Grove.Contents.AuditTrail.Services;
using Grove.Display.Management.Components;
using Grove.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using YesSql;

namespace Grove.Contents.AuditTrail.Api;

/// <summary>
/// A content event's version, for the native audit pages (docs/blazor-display.md rework step 7;
/// replaces the MVC <c>AuditTrail/Content/{Display,Restore}</c> screen): the version the event
/// recorded, rebuilt from its diff and shown as a display tree under the caller's
/// <c>EditContent</c>, and its restore under <c>PublishContent</c>
/// (<c>api/grove/audit/content/{eventId}</c>).
/// </summary>
public sealed class AuditTrailContentApi : ApiContributor
{
    public override string Name => "AuditTrailContent";

    public override string FeatureId => "Grove.Contents";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("audit/content/{eventId}");
        routes.MapGet("", (HttpContext http, [FromRoute] string eventId) => ApiHandlers.For<AuditTrailContentHandlers>(http).GetAsync(eventId));
        routes.MapGet("diff", (HttpContext http, [FromRoute] string eventId) => ApiHandlers.For<AuditTrailContentHandlers>(http).DiffAsync(eventId));
        routes.MapPost("restore", (HttpContext http, [FromRoute] string eventId) => ApiHandlers.For<AuditTrailContentHandlers>(http).RestoreAsync(eventId));
    }
}

public sealed class AuditTrailContentHandlers(
    YesSql.ISession session,
    IContentManager contentManager,
    IAuthorizationService authorizationService,
    ContentHistory history,
    IContentItemDisplayManager displayManager,
    IDisplayTreeBuilder trees,
    IDisplayTreeFilter filter,
    ICallerContextAccessor callers) : ApiHandlers
{
    public async Task<IResult> GetAsync(string eventId)
    {
        var cancellationToken = HttpContext.RequestAborted;
        var (auditEvent, contentItem) = await RebuildAsync(eventId);
        if (auditEvent?.As<AuditTrailContentEvent>() is not { } contentEvent || contentItem is null)
        {
            return NotFound();
        }

        contentItem.Id = 0;
        contentItem.ContentItemVersionId = string.Empty;
        contentItem.Published = false;
        contentItem.Latest = false;
        contentItem = await contentManager.LoadAsync(contentItem);

        if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, contentItem))
        {
            return Forbid();
        }

        var shape = await displayManager.BuildDisplayAsync(contentItem, PlatformConstants.DisplayType.Detail);
        var tree = await trees.BuildAsync(shape, cancellationToken: cancellationToken);
        var root = await filter.FilterAsync(tree.Root, callers.Current, cancellationToken) ?? DisplayNode.Markup(tree.Root.Id, string.Empty);
        Response.Headers.CacheControl = "no-store";
        return Ok(new AuditContentVersion(contentItem.ContentItemId, contentItem.ContentType, contentItem.DisplayText, contentEvent.VersionNumber, new DisplayTree { Root = root, DisplayType = PlatformConstants.DisplayType.Detail, ScopeSignature = tree.ScopeSignature }));
    }

    /// <summary>The changes this event made: the version it recorded and the one before it, as content JSON (<c>ContentDiff</c>).</summary>
    public async Task<IResult> DiffAsync(string eventId)
    {
        var (auditEvent, contentItem) = await RebuildAsync(eventId);
        if (auditEvent is null || contentItem is null)
        {
            return NotFound();
        }

        if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, await contentManager.LoadAsync(contentItem)))
        {
            return Forbid();
        }

        var previous = await history.RebuildPreviousAsync(auditEvent);
        Response.Headers.CacheControl = "no-store";
        return Ok(new AuditContentDiff(
            previous is null ? null : System.Text.Json.JsonSerializer.SerializeToNode(previous, System.Text.Json.JOptions.Default),
            System.Text.Json.JsonSerializer.SerializeToNode(contentItem, System.Text.Json.JOptions.Default)));
    }

    public async Task<IResult> RestoreAsync(string eventId)
    {
        var (_, contentItem) = await RebuildAsync(eventId);
        if (contentItem is null)
        {
            return NotFound();
        }

        contentItem = await contentManager.LoadAsync(contentItem);
        if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.PublishContent, contentItem))
        {
            return Forbid();
        }

        var result = await contentManager.RestoreAsync(contentItem);
        return result.Succeeded
            ? Ok(new AuditContentRestoreResult(true, contentItem.ContentItemId, []))
            : Conflict(new AuditContentRestoreResult(false, contentItem.ContentItemId, [.. result.Errors.Select(error => error.ErrorMessage ?? string.Empty)]));
    }

    private async Task<(AuditTrailEvent? Event, ContentItem? Item)> RebuildAsync(string eventId)
    {
        var auditEvent = await session.Query<AuditTrailEvent, AuditTrailEventIndex>(collection: AuditTrailEvent.Collection)
            .Where(index => index.EventId == eventId)
            .FirstOrDefaultAsync();

        // The stored form is a diff: rebuild the version the event recorded.
        return (auditEvent, auditEvent is null ? null : await history.RebuildAsync(auditEvent));
    }
}

/// <summary>A content event's version as the audit pages show it.</summary>
public sealed record AuditContentVersion(string ContentItemId, string ContentType, string? DisplayText, int VersionNumber, DisplayTree Tree);

/// <summary>A content event's version and the one before it (none for the first), as content JSON.</summary>
public sealed record AuditContentDiff(System.Text.Json.Nodes.JsonNode? Before, System.Text.Json.Nodes.JsonNode? After);

public sealed record AuditContentRestoreResult(bool Restored, string ContentItemId, IReadOnlyList<string> Errors);
