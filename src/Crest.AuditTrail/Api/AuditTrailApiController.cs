#nullable enable

using Crest.Access;
using Microsoft.AspNetCore.Mvc;

namespace Crest.AuditTrail.Api;

/// <summary>
/// The audit pages' API (docs/audit.md › step 4): the trail, one event, a record's history and
/// the impersonation sessions. Tenant staff with the audit permission read the tenant's trail;
/// members never do (the permission is under the member class ceiling), and an organization's
/// member admin sees nothing of it in the first cut.
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route(AuditTrailRoutes.Api)]
public sealed class AuditTrailApiController(
    AuditTrailFeedService feed,
    IAccessDecision decision,
    ICallerContextAccessor callers) : ControllerBase
{
    [HttpGet("events")]
    public async Task<IActionResult> EventsAsync([FromQuery] AuditTrailEventFilter filter)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        return Ok(await feed.QueryAsync(filter));
    }

    [HttpGet("events/{eventId}")]
    public async Task<IActionResult> EventAsync(string eventId)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        return await feed.GetAsync(eventId) is { } model ? Ok(model) : NotFound();
    }

    [HttpGet("records/{recordId}")]
    public async Task<IActionResult> RecordAsync(string recordId, [FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        return Ok(await feed.RecordHistoryAsync(recordId, skip, take));
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> SessionsAsync([FromQuery] AuditTrailEventFilter filter)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        return Ok(await feed.SessionsAsync(filter));
    }

    [HttpGet("categories")]
    public async Task<IActionResult> CategoriesAsync()
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        return Ok(feed.Categories());
    }

    private Task<bool> CanViewAsync() =>
        decision.AllowsAsync(callers.Current, AuditTrailPermissions.ViewAuditTrail.Name, cancellationToken: HttpContext.RequestAborted);
}

/// <summary>The audit pages take the audit permission, as the API behind them does.</summary>
public sealed class AuditTrailRoutePermissionProvider : IRoutePermissionProvider
{
    public IEnumerable<RoutePermission> GetRoutes() =>
    [
        new(AuditTrailRoutes.TrailAdmin, AuditTrailPermissions.ViewAuditTrail.Name),
        new(AuditTrailRoutes.SessionsAdmin, AuditTrailPermissions.ViewAuditTrail.Name),
        new(AuditTrailRoutes.RecordAdmin, AuditTrailPermissions.ViewAuditTrail.Name),
    ];
}
