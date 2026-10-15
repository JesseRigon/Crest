using Microsoft.Extensions.Options;
using Grove.ResourceManagement;

namespace Grove.Notifications;

public sealed class NotificationOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest s_manifest;

    static NotificationOptionsConfiguration()
    {
        s_manifest = new ResourceManifest();

        s_manifest
            .DefineScript("notification-manager")
            .SetUrl("~/Grove.Notifications/Scripts/notification-manager.min.js", "~/Grove.Notifications/Scripts/notification-manager.js")
            .SetVersion("1.0.0");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(s_manifest);
    }
}
