using Fluid;
using Microsoft.Extensions.Options;
using Grove.DisplayManagement.Liquid;
using Grove.Liquid.Endpoints.Scripts;
using Grove.ResourceManagement;

namespace Grove.Liquid;

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
                "~/Grove.Liquid/monaco/liquid-intellisense.min.js",
                "~/Grove.Liquid/monaco/liquid-intellisense.js"
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
            .SetUrl($"~/Grove.Liquid/Scripts/liquid-intellisense.js?v={hash}");

        options.ResourceManifests.Add(s_manifest);
        options.ResourceManifests.Add(manifest);
    }
}
