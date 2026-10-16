using Fluid;
using Fluid.Values;
using Grove.Contents.Management;
using Grove.Display.Liquid;
using Grove.Contents.Hierarchy.Models;

namespace Grove.Contents.Hierarchy.Liquid;

public class ContainerFilter : ILiquidFilter
{
    private readonly IContentManager _contentManager;

    public ContainerFilter(IContentManager contentManager)
    {
        _contentManager = contentManager;
    }

    public async ValueTask<FluidValue> ProcessAsync(FluidValue input, FilterArguments arguments, LiquidTemplateContext ctx)
    {
        var contentItem = input.ToObjectValue() as ContentItem ?? throw new ArgumentException("A Content Item was expected");

        var containerId = contentItem.TryGet<ParentContentItemFieldSet>(out var parentContentItemFieldSet) ? parentContentItemFieldSet.ListContentItemId : null;

        if (containerId != null)
        {
            var container = await _contentManager.GetAsync(containerId);

            if (container != null)
            {
                return new ObjectValue(container);
            }
        }

        return new ObjectValue(contentItem);
    }
}
