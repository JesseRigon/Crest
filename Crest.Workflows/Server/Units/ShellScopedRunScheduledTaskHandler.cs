using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Mediator.Models;
using Crest.Workflows.Scheduling;
using Crest.Workflows.Scheduling.Commands;
using Crest.Environment.Shell;

namespace Crest.Workflows.Units;

/// <summary>
/// Replaces the engine's handler for scheduled tasks (timers, cron, delays, scheduled
/// bookmarks): the engine runs them in a bare service scope, where no Crest session ever
/// commits and nothing after-commit runs. Here each task gets a shell scope of its own - a
/// unit (docs/workflows.md › Posting on workflows): the burst commits when the scope ends,
/// queued stimuli and recorded external calls go out after that commit, a failed unit is
/// discarded.
/// </summary>
public sealed class ShellScopedRunScheduledTaskHandler(IShellHost shellHost, ShellSettings shellSettings) : ICommandHandler<RunScheduledTask>
{
    public async Task<Unit> HandleAsync(RunScheduledTask command, CancellationToken cancellationToken)
    {
        var scope = await shellHost.GetScopeAsync(shellSettings);
        await scope.UsingAsync(shellScope => command.Task.ExecuteAsync(new TaskExecutionContext(shellScope.ServiceProvider, cancellationToken)).AsTask());
        return Unit.Instance;
    }
}
