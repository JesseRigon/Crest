using Grove.Environment.Extensions.Features;
using Grove.Environment.Shell;

namespace Grove.Sharing.Services;

/// <summary>
/// Turning sharing on over existing objects: the access index is rebuilt in the background
/// from every kind's roots (<see cref="IShareableRoots"/>). Until an object is indexed it is in
/// no list (requirement 82); single decisions are resolved live and need no index.
/// </summary>
/// <remarks>
/// Only a RE-enable queues the rebuild here. The feature events of an enable run (installed,
/// then enabled, one handler instance per run) fire before the tenant's next activation runs
/// the data migrations, so on a first install the sharing tables do not exist yet: the
/// migration queues that first rebuild itself, as a deferred task once its tables exist.
/// </remarks>
public sealed class SharingFeatureEvents(IShareAccessIndex index) : FeatureEventHandler
{
    public const string FeatureId = "Grove.Sharing";

    private bool _installedInThisRun;

    public override Task InstalledAsync(IFeatureInfo feature)
    {
        if (feature.Id == FeatureId)
        {
            _installedInThisRun = true;
        }

        return Task.CompletedTask;
    }

    public override Task EnabledAsync(IFeatureInfo feature) =>
        feature.Id == FeatureId && !_installedInThisRun ? index.QueueRebuildAsync() : Task.CompletedTask;
}
