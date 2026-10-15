using Microsoft.Extensions.Options;
using Grove.DisplayManagement;

namespace Grove.Settings;

public sealed class ShapeRenderingOptionsConfiguration : IConfigureOptions<ShapeRenderingOptions>
{
    private readonly ISiteService _siteService;

    public ShapeRenderingOptionsConfiguration(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public void Configure(ShapeRenderingOptions options)
    {
        var settings = _siteService.GetSettings<DebugSettings>();

        options.WriteShapeDebugInformation = settings.WriteShapeDebugInformation;
    }
}
