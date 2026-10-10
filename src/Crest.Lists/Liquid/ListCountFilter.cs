using Fluid;
using Fluid.Values;
using Crest.ContentManagement;
using Crest.Liquid;
using Crest.Lists.Helpers;
using YesSql;

namespace Crest.Lists.Liquid;

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
