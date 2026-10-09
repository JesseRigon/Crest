using Microsoft.Extensions.Options;
using Crest.ResourceManagement;

namespace Crest.Notifications;

public sealed class NotificationOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest s_manifest;

    static NotificationOptionsConfiguration()
    {
        s_manifest = new ResourceManifest();

        s_manifest
            .DefineScript("notification-manager")
            .SetUrl("~/Crest.Notifications/Scripts/notification-manager.min.js", "~/Crest.Notifications/Scripts/notification-manager.js")
            .SetVersion("1.0.0");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(s_manifest);
    }
}
