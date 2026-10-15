using Microsoft.Extensions.Options;
using Grove.ResourceManagement;

namespace Grove.Flows;

public sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest s_manifest;

    static ResourceManagementOptionsConfiguration()
    {
        s_manifest = new ResourceManifest();

        s_manifest
            .DefineStyle("flowpart-edit")
            .SetDependencies("widgetslist-edit")
            .SetUrl("~/Grove.Flows/Styles/flows.edit.min.css", "~/Grove.Flows/Styles/flows.edit.css");

        s_manifest
            .DefineScript("flowpart-edit")
            .SetDependencies("Sortable")
            .SetUrl("~/Grove.Flows/Scripts/flows/flows.edit.min.js", "~/Grove.Flows/Scripts/flows/flows.edit.js");

        s_manifest
            .DefineScript("content-type-picker")
            .SetDependencies("vuejs")
            .SetUrl("~/Grove.Flows/Scripts/content-type-picker/content-type-picker.min.js", "~/Grove.Flows/Scripts/content-type-picker/content-type-picker.js");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(s_manifest);
    }
}
