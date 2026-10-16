using Fluid;
using Fluid.Values;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Preview.Models;
using Grove.Contents.Preview.ViewModels;
using Grove.Display.Liquid;

namespace Grove.Contents.Preview.Projections;

/// <summary>An item's preview address (a read projection): the attachment's Liquid pattern rendered for the item.</summary>
public sealed class PreviewProjection(
    ILiquidTemplateManager liquidTemplateManager,
    IContentDefinitionManager contentDefinitionManager) : ContentFieldSetProjection<PreviewFieldSet>
{
    protected override async Task ProjectAsync(ContentItemAspectContext context, PreviewFieldSet part)
    {
        var contentTypeDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(part.ContentItem.ContentType);
        var pattern = contentTypeDefinition?.FieldSets
            .FirstOrDefault(x => string.Equals(x.FieldSetDefinition.Name, "Preview", StringComparison.Ordinal))?
            .GetSettings<PreviewFieldSetSettings>().Pattern;

        if (string.IsNullOrEmpty(pattern))
        {
            return;
        }

        await context.ForAsync<PreviewAspect>(async previewAspect =>
        {
            var model = new PreviewFieldSetViewModel()
            {
                PreviewFieldSet = part,
                ContentItem = part.ContentItem,
            };

            previewAspect.PreviewUrl = await liquidTemplateManager.RenderStringAsync(pattern, NullEncoder.Default, model,
                new Dictionary<string, FluidValue>() { ["ContentItem"] = new ObjectValue(model.ContentItem) });

            previewAspect.PreviewUrl = previewAspect.PreviewUrl.ReplaceLineEndings(string.Empty);
        });
    }
}
