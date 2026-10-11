using Crest.Environment.Extensions.Features;
using Crest.Environment.Shell;

namespace Crest.Sharing.Services;

/// <summary>
/// Turning sharing on over existing objects: the access index is rebuilt in the background
/// from every kind's roots (<see cref="IShareableRoots"/>). Until an object is indexed it is in
/// no list (requirement 82); single decisions are resolved live and need no index.
/// </summary>
public sealed class SharingFeatureEvents(IShareAccessIndex index) : FeatureEventHandler
{
    public const string FeatureId = "Crest.Sharing";

    public override Task EnabledAsync(IFeatureInfo feature) =>
        feature.Id == FeatureId ? index.QueueRebuildAsync() : Task.CompletedTask;
}
