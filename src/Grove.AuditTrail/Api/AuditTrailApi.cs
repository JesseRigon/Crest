#nullable enable

using Grove.Access;
using Grove.Api;
using Grove.AuditTrail.Services;
using Grove.AuditTrail.Settings;
using Grove.Entities;
using Grove.Infrastructure.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.AuditTrail.Api;

/// <summary>
/// <c>api/grove/audit</c>: the audit pages' API (docs/audit.md › step 4): the trail, one event, a
/// record's history and the impersonation sessions, and the settings (which events are recorded
/// and how long they are kept). Tenant staff with the audit permission read the tenant's trail;
/// members never do (the permission is under the member class ceiling), and an organization's
/// member admin sees nothing of it in the first cut.
/// </summary>
public sealed class AuditTrailApi : ApiContributor
{
    public override string Name => "AuditTrail";

    public override string FeatureId => "Grove.AuditTrail";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("audit");
        routes.MapGet("events", (HttpContext http, [AsParameters] AuditTrailEventQuery query) => ApiHandlers.For<AuditTrailHandlers>(http).EventsAsync(query.ToFilter()));
        routes.MapGet("events/{eventId}", (HttpContext http, [FromRoute] string eventId) => ApiHandlers.For<AuditTrailHandlers>(http).EventAsync(eventId));
        routes.MapGet("records/{recordId}", (HttpContext http, [FromRoute] string recordId, [FromQuery] int? skip, [FromQuery] int? take) => ApiHandlers.For<AuditTrailHandlers>(http).RecordAsync(recordId, skip ?? 0, take ?? 50));
        routes.MapGet("sessions", (HttpContext http, [AsParameters] AuditTrailEventQuery query) => ApiHandlers.For<AuditTrailHandlers>(http).SessionsAsync(query.ToFilter()));
        routes.MapGet("categories", ApiHandlers.Handle<AuditTrailHandlers>(h => h.CategoriesAsync()));

        routes.MapGet("settings", ApiHandlers.Handle<AuditTrailSettingsHandlers>(h => h.GetAsync()));
        routes.MapPut("settings", (HttpContext http, [FromBody] AuditTrailSettingsUpdate update) => ApiHandlers.For<AuditTrailSettingsHandlers>(http).PutAsync(update));
    }
}

/// <summary>
/// The query string of the trail and session lists, bound into an <see cref="AuditTrailEventFilter"/>.
/// Every value is optional, as it was under MVC's model binding.
/// </summary>
public sealed class AuditTrailEventQuery
{
    [FromQuery] public string? UserId { get; set; }
    [FromQuery] public string? UserName { get; set; }
    [FromQuery] public string? FunctionalUserId { get; set; }
    [FromQuery] public string? ImpersonatedUserId { get; set; }
    [FromQuery] public bool? ImpersonatedOnly { get; set; }
    [FromQuery] public string? SessionId { get; set; }
    [FromQuery] public string? OrganizationId { get; set; }
    [FromQuery] public string? Category { get; set; }
    [FromQuery] public string? Name { get; set; }
    [FromQuery] public string? RecordId { get; set; }
    [FromQuery] public string? PartyId { get; set; }
    [FromQuery] public DateTime? From { get; set; }
    [FromQuery] public DateTime? To { get; set; }
    [FromQuery] public int? Skip { get; set; }
    [FromQuery] public int? Take { get; set; }

    public AuditTrailEventFilter ToFilter()
    {
        var filter = new AuditTrailEventFilter
        {
            UserId = UserId,
            UserName = UserName,
            FunctionalUserId = FunctionalUserId,
            ImpersonatedUserId = ImpersonatedUserId,
            ImpersonatedOnly = ImpersonatedOnly ?? false,
            SessionId = SessionId,
            OrganizationId = OrganizationId,
            Category = Category,
            Name = Name,
            RecordId = RecordId,
            PartyId = PartyId,
            From = From,
            To = To,
            Skip = Skip ?? 0,
        };
        if (Take is { } take)
        {
            filter.Take = take;
        }

        return filter;
    }
}

public sealed class AuditTrailHandlers(
    AuditTrailFeedService feed,
    IAccessDecision decision,
    ICallerContextAccessor callers) : ApiHandlers
{
    public async Task<IResult> EventsAsync(AuditTrailEventFilter filter)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        return Ok(await feed.QueryAsync(filter));
    }

    public async Task<IResult> EventAsync(string eventId)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        return await feed.GetAsync(eventId) is { } model ? Ok(model) : NotFound();
    }

    public async Task<IResult> RecordAsync(string recordId, int skip = 0, int take = 50)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        return Ok(await feed.RecordHistoryAsync(recordId, skip, take));
    }

    public async Task<IResult> SessionsAsync(AuditTrailEventFilter filter)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        return Ok(await feed.SessionsAsync(filter));
    }

    public async Task<IResult> CategoriesAsync()
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
