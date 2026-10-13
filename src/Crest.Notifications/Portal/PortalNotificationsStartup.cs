using Crest.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Notifications.Portal;

/// <summary>Portal notifications (docs/media.md requirement 30): on by default for a new tenant
/// (the setup recipes enable it), and able to be turned off.</summary>
[Feature(FeatureId)]
public sealed class PortalNotificationsStartup : StartupBase
{
    public const string FeatureId = "Crest.Notifications.Portal";

    public override void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton<IPortalNotifications, PortalNotificationsMarker>();

    private sealed class PortalNotificationsMarker : IPortalNotifications;
}
