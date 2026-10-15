using Grove.Workflows.Studio.Workflows.Designer.Contracts;
using Grove.Workflows.Studio.Workflows.Domain.Contracts;
using Grove.Workflows.Studio.Workflows.UI.Contracts;

namespace Grove.Workflows.Studio.Workflows.Designer.Services;

internal class MapperFactory(IActivityRegistry activityRegistry, IActivityPortService activityPortService, IActivityDisplaySettingsRegistry activityDisplaySettingsRegistry) : IMapperFactory
{
    /// <summary>
    /// Provides the task.
    /// </summary>
    public async Task<IFlowchartMapper> CreateFlowchartMapperAsync(CancellationToken cancellationToken = default)
    {
        var activityMapper = await CreateActivityMapperAsync(cancellationToken);
        return new FlowchartMapper(activityMapper);
    }

    /// <summary>
    /// Provides the task.
    /// </summary>
    public async Task<IActivityMapper> CreateActivityMapperAsync(CancellationToken cancellationToken = default)
    {
        await activityRegistry.EnsureLoadedAsync(cancellationToken);
        return new ActivityMapper(activityRegistry, activityPortService, activityDisplaySettingsRegistry);
    }
}