using Fluid;
using Microsoft.Extensions.Options;
using Crest.DisplayManagement.Liquid;
using Crest.Liquid.Endpoints.Scripts;
using Crest.ResourceManagement;

namespace Crest.Liquid;

public sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private readonly LiquidViewParser _liquidViewParser;
    private readonly IOptions<TemplateOptions> _templateOptions;
    private static readonly ResourceManifest s_manifest;

    static ResourceManagementOptionsConfiguration()
    {
        s_manifest = new ResourceManifest();

        s_manifest
            .DefineScript("monaco-liquid-intellisense")
            .SetUrl(
                "~/Crest.Liquid/monaco/liquid-intellisense.min.js",
                "~/Crest.Liquid/monaco/liquid-intellisense.js"
            )
            .SetVersion("1.0.0");
    }

    public ResourceManagementOptionsConfiguration(LiquidViewParser liquidViewParser, IOptions<TemplateOptions> templateOptions)
    {
        _liquidViewParser = liquidViewParser;
        _templateOptions = templateOptions;
    }

    public void Configure(ResourceManagementOptions options)
    {
        // The site is restarted when settings change

        var hash = GetIntellisenseEndpoint.HashCacheBustingValues(_liquidViewParser, _templateOptions.Value);

        var manifest = new ResourceManifest();

        manifest
            .DefineScript("liquid-intellisense")
            .SetDependencies("monaco-liquid-intellisense")
            .SetUrl($"~/Crest.Liquid/Scripts/liquid-intellisense.js?v={hash}");

        options.ResourceManifests.Add(s_manifest);
        options.ResourceManifests.Add(manifest);
    }
}
