using Grove.Access.AuditTrail;
using Grove.AuditTrail.Services;
using Grove.AuditTrail.Services.Models;
using Grove.Environment.Shell.Scope;
using Grove.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Access.Services;

/// <summary>
/// What the tenant records beyond the mandatory set (actions and denials), a site setting
/// (docs/workflows.md › Decisions needed › What is audited).
/// </summary>
public sealed class AccessAuditSettings
{
    /// <summary>Record every read, one event per operation per user request, no sampling
    /// (volume is handled by retention).</summary>
    public bool LogReads { get; set; }
}

/// <summary>
/// Actions and denials are always recorded; reads only when the tenant turns read logging
/// on or the read is sensitive (impersonation). A denial is written in a child scope that
/// commits on its own, so a request that fails after it does not roll it back.
/// </summary>
public sealed class AccessAuditor(IServiceProvider services) : IAccessAuditor
{
    public async Task RecordAsync(AccessEvent accessEvent, CancellationToken cancellationToken = default)
    {
        if (accessEvent.IsRead && accessEvent.Verdict == AccessVerdict.Allow
            && accessEvent.Caller.ImpersonatorUserId is null
            && !await LogsReadsAsync())
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

    private async Task<bool> LogsReadsAsync()
    {
        // The site document is cached per tenant and preloaded, so this is a dictionary read.
        var sites = services.GetService<ISiteService>();
        return sites is not null && (await sites.GetSettingsAsync<AccessAuditSettings>()).LogReads;
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
            // A system actor's actions are its account's (requirement 36).
            accessEvent.Caller.ActorId ?? string.Empty,
            accessEvent.Caller.SystemAccountId is { } account ? SystemAccounts.DisplayName(account) : accessEvent.Caller.UserName ?? "anonymous",
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
