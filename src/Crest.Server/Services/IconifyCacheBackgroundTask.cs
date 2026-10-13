using Crest.BackgroundTasks;
using Crest.Iconify;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Crest.Services;

// Keeps the Iconify local mirror fresh. A tenant service is never started as a hosted
// service, so the refresh is a background task; the first sync happens on demand, when the
// store is first asked for an icon or its status (IconifyLocalMirrorStore).
[BackgroundTask(
    Title = "Iconify Cache Refresh",
    Schedule = "0 3 * * *",
    Description = "Refreshes the local Iconify icon-sets mirror.")]
public sealed class IconifyCacheBackgroundTask : IBackgroundTask
{
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        if (!IconifyLocalMirrorBuildOptions.Enabled)
        {
            return;
        }

        var localMirrorStore = serviceProvider.GetRequiredService<IIconifyLocalMirrorStore>();
        var logger = serviceProvider.GetRequiredService<ILogger<IconifyCacheBackgroundTask>>();

        var status = await localMirrorStore.SyncAsync(cancellationToken);
        if (status.IsAvailable)
        {
            logger.LogInformation("Iconify App_Data cache refreshed. Version: {Version}; prefixes: {PrefixCount}; icons: {IconCount}.", status.Version, status.PrefixCount, status.IconCount);
        }
        else if (!string.IsNullOrWhiteSpace(status.LastError))
        {
            logger.LogWarning("Iconify App_Data cache refresh failed: {Error}", status.LastError);
        }
    }
}
