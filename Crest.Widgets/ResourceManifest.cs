using Microsoft.Extensions.Options;
using Crest.ResourceManagement;

namespace Crest.Widgets;

public sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest s_manifest;

    static ResourceManagementOptionsConfiguration()
    {
        s_manifest = new ResourceManifest();

        s_manifest
            .DefineStyle("widgetslist-edit")
            .SetUrl("~/Crest.Widgets/Styles/widgetslist.edit.min.css", "~/Crest.Widgets/Styles/widgetslist.edit.css");

        s_manifest
            .DefineScript("widgetslist-edit")
            .SetDependencies("Sortable")
            .SetUrl("~/Crest.Widgets/Scripts/widgets/widgetslist.edit.min.js", "~/Crest.Widgets/Scripts/widgets/widgetslist.edit.js");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(s_manifest);
    }
}
