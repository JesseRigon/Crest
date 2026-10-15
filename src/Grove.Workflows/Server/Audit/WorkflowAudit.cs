using Grove.Access;
using Grove.AuditTrail.Models;
using Grove.AuditTrail.Services;
using Grove.AuditTrail.Services.Models;
using Grove.Environment.Shell.Scope;
using Grove.Modules;
using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Grove.Workflows.Audit;

/// <summary>
/// Workflow history on the audit system (docs/audit.md, ruling 2026-10-06; docs/workflows.md ›
/// Run history and insight through the audit system): every burst of a run with its journal,
/// every hook attachment run, every inbound webhook delivery - rejected ones included. The
/// insight sections and metrics read these as audit feeds; Workflows keeps only the wiring.
/// </summary>
public sealed class WorkflowAuditTrailEventConfiguration : IConfigureOptions<AuditTrailOptions>
{
    public const string Category = "Workflows";

    public const string RunFinished = nameof(RunFinished);
    public const string RunSuspended = nameof(RunSuspended);
    public const string RunFaulted = nameof(RunFaulted);
    public const string RunCancelled = nameof(RunCancelled);
    public const string HookRan = nameof(HookRan);
    public const string HookFailed = nameof(HookFailed);
    public const string WebhookReceived = nameof(WebhookReceived);
    public const string WebhookRejected = nameof(WebhookRejected);

    public void Configure(AuditTrailOptions options)
    {
        options.For<WorkflowAuditTrailEventConfiguration>(Category, S => S["Workflows"])
            .WithEvent(RunFinished, S => S["Run finished"], S => S["A workflow run finished."], true)
            .WithEvent(RunSuspended, S => S["Run suspended"], S => S["A workflow run is waiting (a bookmark, an approval, a timer)."], true)
            .WithEvent(RunFaulted, S => S["Run faulted"], S => S["A workflow run faulted."], true)
            .WithEvent(RunCancelled, S => S["Run cancelled"], S => S["A workflow run was cancelled."], true)
            .WithEvent(HookRan, S => S["Hook ran"], S => S["A hook attachment ran inside a unit of work."], true)
            .WithEvent(HookFailed, S => S["Hook failed"], S => S["A hook attachment failed."], true)
            .WithEvent(WebhookReceived, S => S["Webhook received"], S => S["An inbound webhook was accepted."], true)
            .WithEvent(WebhookRejected, S => S["Webhook rejected"], S => S["An inbound webhook was refused (unknown connection, bad signature, too large, invalid body)."], true);
    }
}

/// <summary>The data of a workflow event.</summary>
public sealed class WorkflowAuditTrailEvent
{
    public string? InstanceId { get; set; }
    public string? DefinitionId { get; set; }
    public int? DefinitionVersion { get; set; }
    public string? DefinitionName { get; set; }
    public string? CorrelationId { get; set; }
    public string? ParentInstanceId { get; set; }
    public string? Status { get; set; }
    public string? SubStatus { get; set; }
    public double? DurationMilliseconds { get; set; }

    /// <summary>The engine's journal for this burst (its execution log), capped at <see cref="WorkflowAuditRecorder.MaxJournalEntries"/>.</summary>
    public List<WorkflowAuditJournalEntry> Journal { get; set; } = [];

    public bool JournalTruncated { get; set; }

    public List<WorkflowAuditIncident> Incidents { get; set; } = [];

    /// <summary>The hook slot, for a hook run.</summary>
    public string? Slot { get; set; }

    /// <summary>The connection and hook, for a webhook delivery.</summary>
    public string? Connection { get; set; }
    public string? Hook { get; set; }
    public int? StatusCode { get; set; }
    public string? Reason { get; set; }
    public long? Bytes { get; set; }
    public int? Started { get; set; }
}

public sealed record WorkflowAuditJournalEntry(DateTimeOffset Timestamp, string ActivityId, string ActivityType, string? ActivityName, string? EventName, string? Message);

public sealed record WorkflowAuditIncident(string ActivityId, string ActivityType, string Message, DateTimeOffset Timestamp);

/// <summary>
/// Writes workflow events through <see cref="IAuditTrailManager"/> when the audit trail is
/// enabled. A failure (a faulted run, a failed hook, a rejected webhook) is written in a child
/// scope that commits on its own, so a unit that fails after it does not roll it back - the
/// same rule as access denials.
/// </summary>
public sealed class WorkflowAuditRecorder(IServiceProvider services, ICallerContextAccessor callers)
{
    public const int MaxJournalEntries = 200;

    public async Task RecordAsync(string name, string correlationId, WorkflowAuditTrailEvent data, AuditTrailSubject subject, bool isolated)
    {
        if (services.GetService<IAuditTrailManager>() is null)
        {
            return;
        }

        var caller = callers.Current;
        var userId = caller?.UserId ?? string.Empty;
        var userName = caller?.UserName ?? (caller?.IsSystem == true ? "system" : string.Empty);

        if (isolated && ShellScope.Current is not null)
        {
            await ShellScope.UsingChildScopeAsync(scope =>
            {
                // The child scope has no gated caller; carry this one so the actor handlers see it.
                var scopedCallers = scope.ServiceProvider.GetService<ICallerContextAccessor>();
                if (scopedCallers is not null && scopedCallers.Current is null)
                {
                    scopedCallers.Current = caller;
                }

                return WriteAsync(scope.ServiceProvider, name, correlationId, userId, userName, data, subject);
            }, activateShell: false);
            return;
        }

        await WriteAsync(services, name, correlationId, userId, userName, data, subject);
    }

    private static Task WriteAsync(IServiceProvider provider, string name, string correlationId, string userId, string userName, WorkflowAuditTrailEvent data, AuditTrailSubject subject)
    {
        var manager = provider.GetService<IAuditTrailManager>();
        return manager is null
            ? Task.CompletedTask
            : manager.RecordEventAsync(new AuditTrailContext<WorkflowAuditTrailEvent>(name, WorkflowAuditTrailEventConfiguration.Category, correlationId, userId, userName, data)
            {
                Subject = subject,
            });
    }
}

/// <summary>Records each burst of a run when it ends (finished, suspended, faulted, cancelled), with the burst's journal.</summary>
public sealed class WorkflowRunAuditHandler(WorkflowAuditRecorder recorder) : INotificationHandler<WorkflowExecuted>
{
    public Task HandleAsync(WorkflowExecuted notification, CancellationToken cancellationToken)
    {
        var state = notification.WorkflowState;
        var context = notification.WorkflowExecutionContext;
        var name = state.SubStatus switch
        {
            Grove.Workflows.WorkflowSubStatus.Faulted => WorkflowAuditTrailEventConfiguration.RunFaulted,
            Grove.Workflows.WorkflowSubStatus.Cancelled => WorkflowAuditTrailEventConfiguration.RunCancelled,
            Grove.Workflows.WorkflowSubStatus.Finished => WorkflowAuditTrailEventConfiguration.RunFinished,
            _ => WorkflowAuditTrailEventConfiguration.RunSuspended,
        };

        var journal = context.ExecutionLog.OrderBy(entry => entry.Sequence).ToArray();
        var definitionName = notification.Workflow.WorkflowMetadata?.Name ?? state.Name;
        var data = new WorkflowAuditTrailEvent
        {
            InstanceId = state.Id,
            DefinitionId = state.DefinitionId,
            DefinitionVersion = state.DefinitionVersion,
            DefinitionName = definitionName,
            CorrelationId = state.CorrelationId,
            ParentInstanceId = state.ParentWorkflowInstanceId,
            Status = state.Status.ToString(),
            SubStatus = state.SubStatus.ToString(),
            DurationMilliseconds = journal.Length > 1 ? (journal[^1].Timestamp - journal[0].Timestamp).TotalMilliseconds : null,
            Journal = journal.Take(WorkflowAuditRecorder.MaxJournalEntries)
                .Select(entry => new WorkflowAuditJournalEntry(entry.Timestamp, entry.ActivityId, entry.ActivityType, entry.ActivityName, entry.EventName, entry.Message))
                .ToList(),
            JournalTruncated = journal.Length > WorkflowAuditRecorder.MaxJournalEntries,
            Incidents = state.Incidents.Select(incident => new WorkflowAuditIncident(incident.ActivityId, incident.ActivityType, incident.Message, incident.Timestamp)).ToList(),
        };

        // The run is the record; the object the run is about (its correlation id) is the party
        // or document a feed filters on downstream.
        var subject = new AuditTrailSubject { RecordId = state.Id, RecordType = "WorkflowInstance", DisplayText = definitionName };

        return recorder.RecordAsync(name, state.Id, data, subject, isolated: state.SubStatus == Grove.Workflows.WorkflowSubStatus.Faulted);
    }
}

/// <summary>The recorder is always there (a no-op while the audit trail is disabled), so the hook runner and the webhook endpoint can use it.</summary>
[Feature("Grove.Workflows")]
public sealed class AuditRecorderStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services) => services.AddScoped<WorkflowAuditRecorder>();
}

[Feature("Grove.Workflows")]
[RequireFeatures("Grove.AuditTrail")]
public sealed class AuditStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<AuditTrailOptions>, WorkflowAuditTrailEventConfiguration>();
        services.AddNotificationHandler<WorkflowRunAuditHandler>();
    }
}
