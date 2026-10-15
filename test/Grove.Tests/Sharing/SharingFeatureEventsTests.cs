using Grove.Environment.Extensions.Features;
using Grove.Sharing;
using Grove.Sharing.Services;
using Moq;

namespace Grove.Tests.Sharing;

/// <summary>
/// The feature events of an enable run fire before the tenant's next activation runs the data
/// migrations: on a first install the sharing tables do not exist yet, so only a re-enable
/// queues the access-index rebuild here (the migration queues the first one itself).
/// </summary>
public class SharingFeatureEventsTests
{
    private static IFeatureInfo Feature(string id) => Mock.Of<IFeatureInfo>(f => f.Id == id);

    [Fact]
    public async Task FirstInstall_DoesNotQueueTheRebuild()
    {
        var index = new Mock<IShareAccessIndex>();
        var events = new SharingFeatureEvents(index.Object);
        var sharing = Feature(SharingFeatureEvents.FeatureId);

        await events.InstalledAsync(sharing);
        await events.EnabledAsync(sharing);

        index.Verify(i => i.QueueRebuildAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReEnable_QueuesTheRebuild()
    {
        var index = new Mock<IShareAccessIndex>();
        index.Setup(i => i.QueueRebuildAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var events = new SharingFeatureEvents(index.Object);

        await events.InstalledAsync(Feature("Grove.Other"));
        await events.EnabledAsync(Feature(SharingFeatureEvents.FeatureId));

        index.Verify(i => i.QueueRebuildAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
