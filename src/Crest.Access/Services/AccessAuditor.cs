using Crest.Access.AuditTrail;
using Crest.AuditTrail.Services;
using Crest.AuditTrail.Services.Models;
using Crest.Environment.Shell.Scope;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crest.Access.Services;

/// <summary>What the tenant records beyond the mandatory set (actions and denials).</summary>
public sealed class AccessAuditOptions
{
    /// <summary>Record every read as its own event (volume is handled by retention).</summary>
    public bool LogReads { get; set; }
}

/// <summary>
/// Actions and denials are always recorded; reads only when the tenant turns read logging
/// on or the read is sensitive (impersonation). A denial is written in a child scope that
/// commits on its own, so a request that fails after it does not roll it back.
/// </summary>
public sealed class AccessAuditor(IServiceProvider services, IOptions<AccessAuditOptions> options) : IAccessAuditor
{
    public async Task RecordAsync(AccessEvent accessEvent, CancellationToken cancellationToken = default)
    {
        if (accessEvent.IsRead && accessEvent.Verdict == AccessVerdict.Allow
            && !options.Value.LogReads
            && accessEvent.Caller.ImpersonatorUserId is null)
        {
            return;
        }

        if (accessEvent.Verdict != AccessVerdict.Allow && ShellScope.Current is not null)
        {
            await ShellScope.UsingChildScopeAsync(scope => WriteAsync(scope.ServiceProvider, accessEvent), activateShell: false);
            return;
        }

        await WriteAsync(services, accessEvent);
    }

    private static async Task WriteAsync(IServiceProvider provider, AccessEvent accessEvent)
    {
        var manager = provider.GetService<IAuditTrailManager>();
        if (manager is null)
        {
            return;
        }

        var name = accessEvent.Kind == AccessEventKind.Decision
            ? (accessEvent.Verdict == AccessVerdict.Allow ? AccessAuditTrailEventConfiguration.Allowed : AccessAuditTrailEventConfiguration.Denied)
            : AccessAuditTrailEventConfiguration.Executed;

        await manager.RecordEventAsync(new AuditTrailContext<AccessAuditTrailEvent>(
            name,
            AccessAuditTrailEventConfiguration.Category,
            accessEvent.Resource ?? accessEvent.Operation,
            accessEvent.Caller.UserId ?? string.Empty,
            accessEvent.Caller.UserName ?? (accessEvent.Caller.IsSystem ? "system" : "anonymous"),
            new AccessAuditTrailEvent
            {
                Operation = accessEvent.Operation,
                Verdict = accessEvent.Verdict.ToString(),
                Side = accessEvent.Caller.Side.ToString(),
                OrganizationId = accessEvent.Caller.OrganizationId,
                ImpersonatorUserId = accessEvent.Caller.ImpersonatorUserId,
                IsSystem = accessEvent.Caller.IsSystem,
                IsRead = accessEvent.IsRead,
                Resource = accessEvent.Resource,
                ScopeSignature = accessEvent.ScopeSignature,
                Reason = accessEvent.Reason,
                DurationMilliseconds = accessEvent.DurationMilliseconds,
            }));
    }
}
