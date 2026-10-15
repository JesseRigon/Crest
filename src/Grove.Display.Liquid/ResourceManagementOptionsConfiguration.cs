using Fluid;
using Microsoft.Extensions.Options;
using Grove.Display.Liquid.Services;
using Grove.Display.Liquid.Endpoints.Scripts;
using Grove.Display.Resources.Management;

namespace Grove.Display.Liquid;

public sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private readonly LiquidParser _liquidParser;
    private readonly IOptions<TemplateOptions> _templateOptions;
    private static readonly ResourceManifest s_manifest;

    static ResourceManagementOptionsConfiguration()
    {
        s_manifest = new ResourceManifest();

        s_manifest
            .DefineScript("monaco-liquid-intellisense")
            .SetUrl(
                "~/Grove.Display.Liquid/monaco/liquid-intellisense.min.js",
                "~/Grove.Display.Liquid/monaco/liquid-intellisense.js"
            )
            .SetVersion("1.0.0");
    }

    public ResourceManagementOptionsConfiguration(LiquidParser liquidParser, IOptions<TemplateOptions> templateOptions)
    {
        _liquidParser = liquidParser;
        _templateOptions = templateOptions;
    }

    public void Configure(ResourceManagementOptions options)
    {
        // The site is restarted when settings change

        var hash = GetIntellisenseEndpoint.HashCacheBustingValues(_liquidParser, _templateOptions.Value);

        var manifest = new ResourceManifest();

        manifest
            .DefineScript("liquid-intellisense")
            .SetDependencies("monaco-liquid-intellisense")
            .SetUrl($"~/Grove.Display.Liquid/Scripts/liquid-intellisense.js?v={hash}");

        options.ResourceManifests.Add(s_manifest);
        options.ResourceManifests.Add(manifest);
    }
}
