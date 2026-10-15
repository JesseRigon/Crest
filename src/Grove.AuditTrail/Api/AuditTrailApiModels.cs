#nullable enable

using System.Text.Json.Nodes;

namespace Grove.AuditTrail.Api;

/// <summary>
/// The filters of the audit page (docs/audit.md › step 4): actor, impersonated member,
/// organization, category, record and date, plus the impersonation session and a party.
/// Every filter is applied in the query, before paging.
/// </summary>
public sealed class AuditTrailEventFilter
{
    /// <summary>The audit attribution: who really acted (a user id or a client id).</summary>
    public string? UserId { get; set; }

    /// <summary>A user name, matched against the actor's normalized name.</summary>
    public string? UserName { get; set; }

    /// <summary>The functional attribution: who the record belongs to (the impersonated member).</summary>
    public string? FunctionalUserId { get; set; }

    public string? ImpersonatedUserId { get; set; }

    public bool ImpersonatedOnly { get; set; }

    public string? SessionId { get; set; }

    public string? OrganizationId { get; set; }

    public string? Category { get; set; }

    public string? Name { get; set; }

    public string? RecordId { get; set; }

    public string? PartyId { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public int Skip { get; set; }

    public int Take { get; set; } = 50;
}

public sealed record AuditTrailEventModel(
    string EventId,
    string Category,
    string CategoryText,
    string Name,
    string NameText,
    string? CorrelationId,
    string? UserId,
    string? UserName,
    DateTime CreatedUtc,
    string? ClientIpAddress,
    string ActorKind,
    string? ClientId,
    string? Side,
    string? OrganizationId,
    string? FunctionalUserId,
    string? FunctionalUserName,
    string? ImpersonatedUserId,
    string? ImpersonatedUserName,
    string? SessionId,
    string? RecordId,
    string? RecordType,
    string? OwnerId,
    IReadOnlyList<string> PartyIds,
    string Summary,
    JsonObject? Data);

public sealed record AuditTrailEventPage(IReadOnlyList<AuditTrailEventModel> Items, int Total, int Skip, int Take);

public sealed record AuditTrailCategoryModel(string Name, string Text, IReadOnlyList<AuditTrailEventNameModel> Events);

public sealed record AuditTrailEventNameModel(string Name, string Text);

/// <summary>One impersonation session: its start, its stop (when it was stopped) and how many events it holds.</summary>
public sealed record AuditTrailSessionModel(
    string SessionId,
    string? StaffUserId,
    string? StaffUserName,
    string? MemberUserId,
    string? MemberUserName,
    string? OrganizationId,
    DateTime StartedUtc,
    DateTime? StoppedUtc,
    int EventCount);

public sealed record AuditTrailSessionPage(IReadOnlyList<AuditTrailSessionModel> Items, int Total, int Skip, int Take);
