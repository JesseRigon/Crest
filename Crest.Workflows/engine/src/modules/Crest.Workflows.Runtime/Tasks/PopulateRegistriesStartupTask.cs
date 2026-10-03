using Crest.Workflows.Common;
using Crest.Workflows.Common.RecurringTasks;
using JetBrains.Annotations;

namespace Crest.Workflows.Runtime.Tasks;

/// <summary>
/// Updates the workflow store from <see cref="IWorkflowsProvider"/> implementations, creates triggers and updates the <see cref="IActivityRegistry"/>.
/// </summary>
[UsedImplicitly]
[SingleNodeTask]
public class PopulateRegistriesStartupTask(IRegistriesPopulator registriesPopulator) : IStartupTask
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await registriesPopulator.PopulateAsync(cancellationToken);
    }
}