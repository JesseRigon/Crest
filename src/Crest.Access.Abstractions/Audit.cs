namespace Crest.Access;

public enum AccessEventKind
{
    Decision,
    Execution,
}

/// <summary>
/// One access event: a decision (allow, deny) or an execution (an operation ran). Actions
/// and denials are always recorded; reads only when the tenant turns read logging on or
/// the read is sensitive (impersonation, an external connection, a read job, a flagged
/// type).
/// </summary>
public sealed record AccessEvent(
    AccessEventKind Kind,
    string Operation,
    CallerContext Caller,
    AccessVerdict Verdict,
    string? Resource = null,
    string? ScopeSignature = null,
    string? Reason = null,
    double? DurationMilliseconds = null,
    bool IsRead = false);

public interface IAccessAuditor
{
    Task RecordAsync(AccessEvent accessEvent, CancellationToken cancellationToken = default);
}
