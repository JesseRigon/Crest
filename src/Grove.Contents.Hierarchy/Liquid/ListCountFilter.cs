using Fluid;
using Fluid.Values;
using Grove.Contents.Management;
using Grove.Display.Liquid;
using Grove.Contents.Hierarchy.Helpers;
using YesSql;

namespace Grove.Contents.Hierarchy.Liquid;

public class ListCountFilter : ILiquidFilter
{
    private readonly ISession _session;

    public ListCountFilter(ISession session)
    {
        _session = session;
    }

    public async ValueTask<FluidValue> ProcessAsync(FluidValue input, FilterArguments arguments, LiquidTemplateContext ctx)
    {
        string listContentItemId;

        if (input.Type == FluidValues.Object && input.ToObjectValue() is ContentItem contentItem)
        {
            listContentItemId = contentItem.ContentItemId;
        }
        else
        {
            listContentItemId = input.ToStringValue();
        }

        var listCount = await ListQueryHelpers.QueryListItemsCountAsync(_session, listContentItemId);

        return NumberValue.Create(listCount);
    }
}
