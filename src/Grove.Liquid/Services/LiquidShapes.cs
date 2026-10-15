using System.Text.Encodings.Web;
using Fluid.Values;
using Microsoft.Extensions.DependencyInjection;
using Grove.DisplayManagement.Descriptors;
using Grove.DisplayManagement.Implementation;
using Grove.Liquid.ViewModels;
using Grove.Modules;

namespace Grove.Liquid.Services;

[RequireFeatures("Grove.Contents")]
public class LiquidShapes(HtmlEncoder htmlEncoder) : ShapeTableProvider
{
    private readonly HtmlEncoder _htmlEncoder = htmlEncoder;

    public override ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe("LiquidPart").OnProcessing(BuildViewModelAsync);
        builder.Describe("LiquidPart_Summary").OnProcessing(BuildViewModelAsync);

        return ValueTask.CompletedTask;
    }

    private async Task BuildViewModelAsync(ShapeDisplayContext shapeDisplayContext)
    {
        var model = shapeDisplayContext.Shape as LiquidPartViewModel;

        if (model?.LiquidPart is null)
        {
            return;
        }

        var liquidTemplateManager = shapeDisplayContext.ServiceProvider.GetRequiredService<ILiquidTemplateManager>();

        model.Html = await liquidTemplateManager.RenderStringAsync(model.LiquidPart.Liquid, _htmlEncoder, shapeDisplayContext.DisplayContext.Value,
            new Dictionary<string, FluidValue>()
            {
                ["ContentItem"] = new ObjectValue(model.ContentItem),
            });
    }
}
