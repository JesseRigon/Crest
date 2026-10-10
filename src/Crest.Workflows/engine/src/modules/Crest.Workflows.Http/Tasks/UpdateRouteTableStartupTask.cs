using Crest.Workflows.Common;
using JetBrains.Annotations;

namespace Crest.Workflows.Http.Tasks;

/// <summary>
/// Update the route table based on workflow triggers and bookmarks.
/// </summary>
[UsedImplicitly]
public class UpdateRouteTableStartupTask(IRouteTableUpdater routeTableUpdater) : IStartupTask
{
    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await routeTableUpdater.UpdateAsync(stoppingToken);
    }
}