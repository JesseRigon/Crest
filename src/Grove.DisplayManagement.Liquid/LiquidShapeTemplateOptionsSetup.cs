using Fluid;
using Microsoft.Extensions.Options;
using Grove.DisplayManagement.Descriptors.ShapeTemplateStrategy;

namespace Grove.DisplayManagement.Liquid;

public sealed class LiquidShapeTemplateOptionsSetup : IConfigureOptions<ShapeTemplateOptions>
{
    private readonly TemplateOptions _templateOptions;

    public LiquidShapeTemplateOptionsSetup(IOptions<TemplateOptions> templateOptions)
    {
        _templateOptions = templateOptions.Value;
    }

    public void Configure(ShapeTemplateOptions options)
    {
        options.FileProviders.Insert(0, _templateOptions.FileProvider);
    }
}
