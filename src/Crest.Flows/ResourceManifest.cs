using Microsoft.Extensions.Options;
using Crest.ResourceManagement;

namespace Crest.Flows;

public sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest s_manifest;

    static ResourceManagementOptionsConfiguration()
    {
        s_manifest = new ResourceManifest();

        s_manifest
            .DefineStyle("flowpart-edit")
            .SetDependencies("widgetslist-edit")
            .SetUrl("~/Crest.Flows/Styles/flows.edit.min.css", "~/Crest.Flows/Styles/flows.edit.css");

        s_manifest
            .DefineScript("flowpart-edit")
            .SetDependencies("Sortable")
            .SetUrl("~/Crest.Flows/Scripts/flows/flows.edit.min.js", "~/Crest.Flows/Scripts/flows/flows.edit.js");

        s_manifest
            .DefineScript("content-type-picker")
            .SetDependencies("vuejs")
            .SetUrl("~/Crest.Flows/Scripts/content-type-picker/content-type-picker.min.js", "~/Crest.Flows/Scripts/content-type-picker/content-type-picker.js");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(s_manifest);
    }
}
