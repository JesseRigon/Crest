using System.Text.Json.Nodes;

namespace Grove.AuditTrail.BlazorWasm.Models;

// The shapes of api/grove/audit (Grove.AuditTrail › Api/AuditTrailApiModels.cs), as the pages read them.

public sealed record AuditEvent(
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
    JsonObject? Data)
{
    public bool IsImpersonated => ImpersonatedUserId is not null;

    /// <summary>"staff impersonating member in organization", or the actor alone.</summary>
    public string Actor => IsImpersonated
        ? $"{UserName ?? UserId} → {ImpersonatedUserName ?? ImpersonatedUserId}"
        : ActorKind == "application" ? $"{UserName ?? ClientId} ({ClientId})" : UserName ?? UserId ?? ActorKind;
}

public sealed record AuditEventPage(IReadOnlyList<AuditEvent> Items, int Total, int Skip, int Take);

public sealed record AuditCategory(string Name, string Text, IReadOnlyList<AuditEventName> Events);

public sealed record AuditEventName(string Name, string Text);

public sealed record AuditSession(
    string SessionId,
    string? StaffUserId,
    string? StaffUserName,
    string? MemberUserId,
    string? MemberUserName,
    string? OrganizationId,
    DateTime StartedUtc,
    DateTime? StoppedUtc,
    int EventCount);

public sealed record AuditSessionPage(IReadOnlyList<AuditSession> Items, int Total, int Skip, int Take);

/// <summary>The filters of the audit page, as query string parameters of the events API.</summary>
public sealed class AuditFilter
{
    public string? UserName { get; set; }
    public string? UserId { get; set; }
    public string? ImpersonatedUserId { get; set; }
    public bool ImpersonatedOnly { get; set; }
    public string? SessionId { get; set; }
    public string? OrganizationId { get; set; }
    public string? Category { get; set; }
    public string? RecordId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 50;

    public string ToQueryString()
    {
        var parts = new List<string>();
        void Add(string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
            }
        }

        Add("userName", UserName);
        Add("userId", UserId);
        Add("impersonatedUserId", ImpersonatedUserId);
        Add("sessionId", SessionId);
        Add("organizationId", OrganizationId);
        Add("category", Category);
        Add("recordId", RecordId);
        Add("from", From?.ToUniversalTime().ToString("o"));
        // "To" is a day: the whole of it is included.
        Add("to", To?.Date.AddDays(1).AddTicks(-1).ToUniversalTime().ToString("o"));
        if (ImpersonatedOnly)
        {
            parts.Add("impersonatedOnly=true");
        }

        parts.Add($"skip={Skip}");
        parts.Add($"take={Take}");
        return string.Join('&', parts);
    }
}

/// <summary>A content event's version, rebuilt and shown as a display tree.</summary>
/// <summary>A content event's version and the one before it, as content JSON.</summary>
public sealed record AuditContentDiff(System.Text.Json.Nodes.JsonNode? Before, System.Text.Json.Nodes.JsonNode? After);

public sealed record AuditContentVersion(string ContentItemId, string ContentType, string? DisplayText, int VersionNumber, Grove.Display.Management.Components.DisplayTree Tree);

public sealed record AuditContentRestoreResult(bool Restored, string ContentItemId, IReadOnlyList<string> Errors);
