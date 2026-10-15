using System.Diagnostics;
using Grove.Access;
using Grove.Workflows.Contexts;
using Grove.Workflows.Models;
using Grove.Workflows.Pipelines.ActivityExecution;
using Grove.Workflows.Pipelines.WorkflowExecution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grove.Workflows.Security;

/// <summary>
/// The access gate at the workflow level (docs/operations.md step 4): once per burst, the
/// run's caller. Inserted first in the engine's workflow pipeline (after the builder's
/// <c>Reset</c>, before the heartbeat and the exception handlers), so everything the burst
/// does runs as that caller.
/// <list type="bullet">
/// <item>A run's first burst inherits a caller already set for the scope (a request the gate
/// admitted, a hook child inside its parent's burst) - except the system caller, which only a
/// definition published as system may run as: a flow triggered by a background task or a
/// system flow does not become the tenant super user.</item>
/// <item>Otherwise, and on every later burst, the caller comes from the definition (published
/// as system) or from the sealed actor in the instance input, rebuilt with current rights: a
/// resume runs as the run's actor, never as whoever resumed it.</item>
/// <item>On the first burst the actor is bound to the instance (<see cref="WorkflowActorSeal"/>),
/// so no input a client or an author supplies later can replace it.</item>
/// </list>
/// A burst with no caller is refused with the reason. The previous caller is restored after.
/// </summary>
public sealed class WorkflowAccessGateMiddleware(WorkflowMiddlewareDelegate next, ILogger<WorkflowAccessGateMiddleware> logger) : WorkflowExecutionMiddleware(next)
{
    public override async ValueTask InvokeAsync(WorkflowExecutionContext context)
    {
        // The scope the burst runs in: a child shell scope has its own accessor.
        var accessor = context.GetRequiredService<ICallerContextAccessor>();
        var seal = context.GetRequiredService<WorkflowActorSeal>();
        var inherited = accessor.Current;
        var firstBurst = context.SubStatus == WorkflowSubStatus.Pending;
        var runsAsSystem = WorkflowCallerResolver.RunsAsSystem(context.Workflow);

        var caller = firstBurst && inherited is not null && (!inherited.IsSystem || runsAsSystem)
            ? inherited
            : await context.GetRequiredService<WorkflowCallerResolver>().ForRunAsync(context.Workflow, context.Input, context.Id, firstBurst, context.CancellationToken);

        // An anonymous run only where the definition accepts one and only from a public page
        // (docs/access.md, Anonymous actors); every other anonymous run is refused with the reason.
        if (WorkflowCallerResolver.AnonymousRefusal(caller, context.Workflow, firstBurst, StartsAtPageEndpoint(context)) is { } refusal)
        {
            logger.LogWarning("Workflow {Definition} refused for an anonymous caller: {Reason}", context.Workflow.Identity.DefinitionId, refusal);
            throw new WorkflowAccessRefusedException(refusal);
        }

        if (firstBurst && !caller.IsSystem)
        {
            context.Input[WorkflowsConstants.InputKeys.Actor] = seal.Bind(WorkflowUserContext.From(caller), context.Id);
        }

        if (ReferenceEquals(caller, inherited))
        {
            await Next(context);
            return;
        }

        logger.LogDebug("Workflow {Definition} burst runs as {Caller}.", context.Workflow.Identity.DefinitionId, caller.IsSystem ? "system" : caller.UserName ?? "anonymous");
        accessor.Current = caller;
        try
        {
            await Next(context);
        }
        finally
        {
            accessor.Current = inherited;
        }
    }

    /// <summary>Whether the run starts at an HTTP endpoint trigger, the page URL a public flow is served at.</summary>
    private static bool StartsAtPageEndpoint(WorkflowExecutionContext context) =>
        context.TriggerActivityId is { } triggerId && context.FindNodeByActivityId(triggerId)?.Activity is Grove.Workflows.Http.HttpEndpoint;
}

/// <summary>
/// The access gate at the activity level: for an activity whose class declares a permission
/// (<see cref="RequiresPermissionAttribute"/>, carried on the descriptor), the one decision
/// for the burst's caller before the terminal invoker runs it, and an execution record
/// through the auditor whatever the verdict. A denial throws, which the engine's activity
/// exception handling turns into a fault with the reason.
/// </summary>
public sealed class ActivityAccessGateMiddleware(ActivityMiddlewareDelegate next, ILogger<ActivityAccessGateMiddleware> logger) : IActivityExecutionMiddleware
{
    public async ValueTask InvokeAsync(ActivityExecutionContext context)
    {
        var permission = RequiredPermission(context.ActivityDescriptor);
        if (permission is null)
        {
            await next(context);
            return;
        }

        var caller = context.GetRequiredService<ICallerContextAccessor>().Current
            ?? throw new WorkflowAccessRefusedException($"Activity '{context.Activity.Type}' takes {permission} but the burst has no caller; the access gate did not run.");
        var decision = await context.GetRequiredService<IAccessDecision>().DecideAsync(caller, permission, PermissionResource(context.ActivityDescriptor), context.CancellationToken);
        var auditor = context.GetRequiredService<IAccessAuditor>();
        var resource = $"{context.WorkflowExecutionContext.Workflow.Identity.DefinitionId}/{context.Activity.Id}";

        if (!decision.IsAllowed)
        {
            await auditor.RecordAsync(new AccessEvent(AccessEventKind.Execution, context.Activity.Type, caller, decision.Verdict, resource, caller.ScopeSignature, decision.Reason), context.CancellationToken);
            logger.LogWarning("Activity {Activity} of workflow {Definition} denied for {Caller}: {Permission}.", context.Activity.Type, context.WorkflowExecutionContext.Workflow.Identity.DefinitionId, caller.UserName ?? "anonymous", permission);
            throw new WorkflowAccessRefusedException($"'{caller.UserName ?? "anonymous"}' may not run '{context.Activity.Type}': it takes {permission}{(decision.Reason is null ? string.Empty : $" ({decision.Reason})")}.");
        }

        var started = Stopwatch.GetTimestamp();
        await next(context);
        await auditor.RecordAsync(new AccessEvent(AccessEventKind.Execution, context.Activity.Type, caller, AccessVerdict.Allow, resource, caller.ScopeSignature, DurationMilliseconds: Stopwatch.GetElapsedTime(started).TotalMilliseconds), context.CancellationToken);
    }

    public static string? RequiredPermission(ActivityDescriptor descriptor) =>
        descriptor.CustomProperties.TryGetValue(WorkflowsConstants.RequiredPermissionDescriptorProperty, out var value) && value is string permission && permission.Length > 0 ? permission : null;

    /// <summary>What the permission is asked against, when the descriptor names it (a content
    /// operation's type, so the per-type permissions decide); null asks the bare permission.</summary>
    public static string? PermissionResource(ActivityDescriptor descriptor) =>
        descriptor.CustomProperties.TryGetValue(WorkflowsConstants.PermissionResourceDescriptorProperty, out var value) && value is string resource && resource.Length > 0 ? resource : null;
}

/// <summary>Carries an activity class's <see cref="RequiresPermissionAttribute"/> onto its engine descriptor, where the gate and the registry read it.</summary>
public sealed class RequiredPermissionDescriptorModifier : IActivityDescriptorModifier
{
    public void Modify(ActivityDescriptor descriptor)
    {
        if (descriptor.ClrType?.GetCustomAttributes(typeof(RequiresPermissionAttribute), inherit: true).OfType<RequiresPermissionAttribute>().FirstOrDefault() is { } attribute)
        {
            descriptor.CustomProperties[WorkflowsConstants.RequiredPermissionDescriptorProperty] = attribute.Permission;
        }
    }
}
