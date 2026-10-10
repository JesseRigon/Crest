using Crest.Access;
using Crest.Workflows.Contexts;
using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Mediator.Models;
using Crest.Workflows.Scheduling;
using Crest.Workflows.Scheduling.Commands;
using Crest.Workflows.Scheduling.Tasks;
using Crest.Environment.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Units;

/// <summary>
/// Replaces the engine's handler for scheduled tasks (timers, cron, delays, scheduled
/// bookmarks): the engine runs them in a bare service scope, where no Crest session ever
/// commits and nothing after-commit runs. Here each task gets a shell scope of its own - a
/// unit (docs/workflows.md › Posting on workflows): the burst commits when the scope ends,
/// queued stimuli and recorded external calls go out after that commit, a failed unit is
/// discarded. The run acts as the caller the definition names (the system actor for a
/// definition published as system, else the actor persisted in the instance input), resolved
/// from the child scope before the engine starts; a timer-started flow with neither is refused
/// here (docs/operations.md step 4, actors where none exists today).
/// </summary>
public sealed class ShellScopedRunScheduledTaskHandler(IShellHost shellHost, ShellSettings shellSettings) : ICommandHandler<RunScheduledTask>
{
    public async Task<Unit> HandleAsync(RunScheduledTask command, CancellationToken cancellationToken)
    {
        var scope = await shellHost.GetScopeAsync(shellSettings);
        await scope.UsingAsync(async shellScope =>
        {
            var services = shellScope.ServiceProvider;
            var caller = await CallerAsync(services, command.Task, cancellationToken);
            Task Work() => command.Task.ExecuteAsync(new TaskExecutionContext(services, cancellationToken)).AsTask();
            if (caller is null)
            {
                // Not a workflow run (a delegate task): the gate decides per burst, if any.
                await Work();
                return;
            }

            await services.GetRequiredService<IAccessRunner>().RunAsAsync(caller, Work);
        });
        return Unit.Instance;
    }

    private static Task<CallerContext?> CallerAsync(IServiceProvider services, ITask task, CancellationToken cancellationToken)
    {
        var callers = services.GetRequiredService<WorkflowCallerResolver>();
        return task switch
        {
            RunWorkflowTask run => callers.ForDefinitionAsync(run.Request.WorkflowDefinitionHandle, run.Request.Input, cancellationToken),
            ResumeWorkflowTask resume => callers.ForInstanceAsync(resume.Request.WorkflowInstanceId, cancellationToken),
            _ => Task.FromResult<CallerContext?>(null),
        };
    }
}
