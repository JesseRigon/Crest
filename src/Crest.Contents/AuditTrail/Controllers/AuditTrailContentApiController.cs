using Crest.Access;
using Crest.AuditTrail.Indexes;
using Crest.AuditTrail.Models;
using Crest.ContentManagement;
using Crest.ContentManagement.Display;
using Crest.Contents.AuditTrail.Models;
using Crest.Contents.AuditTrail.Services;
using Crest.DisplayManagement.Components;
using Crest.DisplayManagement.ModelBinding;
using Crest.Entities;
using Crest.Modules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YesSql;

namespace Crest.Contents.AuditTrail.Controllers;

/// <summary>
/// A content event's version, for the native audit pages (docs/blazor-display.md rework step 7;
/// replaces the MVC <c>AuditTrail/Content/{Display,Restore}</c> screen): the version the event
/// recorded, rebuilt from its diff and shown as a display tree under the caller's
/// <c>EditContent</c>, and its restore under <c>PublishContent</c>.
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[RequireFeatures("Crest.AuditTrail")]
[Route("api/crest/audit/content/{eventId}")]
public sealed class AuditTrailContentApiController(
    ISession session,
    IContentManager contentManager,
    IAuthorizationService authorizationService,
    ContentHistory history) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AuditContentVersion>> GetAsync(
        string eventId,
        [FromServices] IContentItemDisplayManager displayManager,
        [FromServices] IUpdateModelAccessor updaters,
        [FromServices] IDisplayTreeBuilder trees,
        [FromServices] IDisplayTreeFilter filter,
        [FromServices] ICallerContextAccessor callers,
        CancellationToken cancellationToken)
    {
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

        var shape = await displayManager.BuildDisplayAsync(contentItem, updaters.ModelUpdater, PlatformConstants.DisplayType.Detail);
        var tree = await trees.BuildAsync(shape, cancellationToken: cancellationToken);
        var root = await filter.FilterAsync(tree.Root, callers.Current, cancellationToken) ?? DisplayNode.Markup(tree.Root.Id, string.Empty);
        Response.Headers.CacheControl = "no-store";
        return Ok(new AuditContentVersion(contentItem.ContentItemId, contentItem.ContentType, contentItem.DisplayText, contentEvent.VersionNumber, new DisplayTree { Root = root, DisplayType = PlatformConstants.DisplayType.Detail, ScopeSignature = tree.ScopeSignature }));
    }

    [HttpPost("restore")]
    public async Task<ActionResult<AuditContentRestoreResult>> RestoreAsync(string eventId)
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

public sealed record AuditContentRestoreResult(bool Restored, string ContentItemId, IReadOnlyList<string> Errors);
