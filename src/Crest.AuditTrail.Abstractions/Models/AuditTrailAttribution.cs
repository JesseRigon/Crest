#nullable enable

namespace Crest.AuditTrail.Models;

/// <summary>
/// Who acted, both ways (docs/audit.md › Two attributions), stored on every event next to the
/// stock user fields. The event's <see cref="AuditTrailEvent.UserId"/> is always the audit
/// attribution - who really acted (the staff user under impersonation, an application's client
/// id for a machine). <see cref="FunctionalUserId"/> is the functional attribution - who the
/// record belongs to (the impersonated member). Neither is derived from the other at read time:
/// both are written when the event is recorded, by the actor handlers.
/// </summary>
public sealed class AuditTrailAttribution
{
    /// <summary>One of <see cref="AuditTrailActorKinds"/>.</summary>
    public string ActorKind { get; set; } = AuditTrailActorKinds.User;

    /// <summary>The OpenID client id when an application acted without a user.</summary>
    public string? ClientId { get; set; }

    /// <summary>The shell the request acted in (Site, Admin, Member, System).</summary>
    public string? Side { get; set; }

    /// <summary>The organization the caller acted in (the active organization on the member side).</summary>
    public string? OrganizationId { get; set; }

    /// <summary>The functional attribution: the member under impersonation, otherwise the actor.</summary>
    public string? FunctionalUserId { get; set; }

    public string? FunctionalUserName { get; set; }

    /// <summary>The member a staff user was impersonating; null when nobody was impersonated.</summary>
    public string? ImpersonatedUserId { get; set; }

    public string? ImpersonatedUserName { get; set; }

    /// <summary>
    /// The impersonation session the event happened in: the correlation id of its start and stop
    /// events, so a session lists as one unit with its actions under it.
    /// </summary>
    public string? SessionId { get; set; }

    public bool IsImpersonated => ImpersonatedUserId is not null;
}

public static class AuditTrailActorKinds
{
    public const string User = "user";

    /// <summary>An API client acting without a user (docs/machine-actors.md).</summary>
    public const string Application = "application";

    /// <summary>The tenant or organization system actor, or a background entry point.</summary>
    public const string System = "system";

    public const string Anonymous = "anonymous";
}

/// <summary>
/// What an event is about, so activity feeds are filters over the trail (docs/audit.md › step 5):
/// the record, its type, its owner and the parties it concerns. Filled by whoever records the
/// event and extended by any <see cref="Services.IAuditTrailEventHandler"/> (a module that knows
/// which parties a record concerns adds them in <c>AlterAsync</c>).
/// </summary>
public sealed class AuditTrailSubject
{
    /// <summary>The record's id (a content item id, a user id, a workflow instance id, ...).</summary>
    public string? RecordId { get; set; }

    /// <summary>The record's kind (a content type, <c>User</c>, <c>Role</c>, <c>WorkflowInstance</c>, ...).</summary>
    public string? RecordType { get; set; }

    /// <summary>What a person reads for the record (a content item's display text, a user name).</summary>
    public string? DisplayText { get; set; }

    /// <summary>The functional owner of the record, when it has one.</summary>
    public string? OwnerId { get; set; }

    /// <summary>The parties (content item ids) the event concerns.</summary>
    public List<string> PartyIds { get; set; } = [];
}

/// <summary>
/// Impersonation sessions (docs/audit.md › step 2). A session's start and stop events carry
/// the session id as their correlation id; every event recorded inside the session carries it
/// as <see cref="AuditTrailAttribution.SessionId"/>.
/// </summary>
public static class AuditTrailSessionEvents
{
    public const string Category = "Impersonation";

    public const string Started = "Started";

    public const string Stopped = "Stopped";
}
