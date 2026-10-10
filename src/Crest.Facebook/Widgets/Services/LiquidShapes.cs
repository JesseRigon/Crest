using System.Text.Encodings.Web;
using Fluid.Values;
using Microsoft.Extensions.DependencyInjection;
using Crest.DisplayManagement.Descriptors;
using Crest.DisplayManagement.Implementation;
using Crest.Facebook.Widgets.ViewModels;
using Crest.Liquid;

namespace Crest.Facebook.Widgets.Services;

public class LiquidShapes(HtmlEncoder htmlEncoder) : ShapeTableProvider
{
    private readonly HtmlEncoder _htmlEncoder = htmlEncoder;

    public override ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe("FacebookPluginPart").OnProcessing(BuildViewModelAsync);
        builder.Describe("FacebookPluginPart_Summary").OnProcessing(BuildViewModelAsync);

        return ValueTask.CompletedTask;
    }

    private async Task BuildViewModelAsync(ShapeDisplayContext shapeDisplayContext)
    {
        var model = shapeDisplayContext.Shape as FacebookPluginPartViewModel;
        var liquidTemplateManager = shapeDisplayContext.ServiceProvider.GetRequiredService<ILiquidTemplateManager>();

        model.Html = await liquidTemplateManager.RenderStringAsync(model.FacebookPluginPart.Liquid, _htmlEncoder, shapeDisplayContext.DisplayContext.Value,
            new Dictionary<string, FluidValue>() { ["ContentItem"] = new ObjectValue(model.ContentItem) });
    }
}
