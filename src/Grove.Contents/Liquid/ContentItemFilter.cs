using Grove.Access;
using Grove.Contents.Management;
using Grove.Display.Liquid;
using Fluid;
using Fluid.Values;

namespace Grove.Contents.Liquid;

/// <summary>
/// <c>content_item_id</c>: loads content items by id for a template. Templates are
/// tenant-authored (a title pattern is Liquid too), so this filter is data scope, not a
/// back door: an item reaches the template only when the request's caller may view it,
/// decided by the one access decision; with no caller nothing loads (fail-closed). The
/// sandbox is what a template can REACH, and this is where a template reaches the store
/// (docs/content-engine.md › Liquid over content).
/// </summary>
public class ContentItemFilter : ILiquidFilter
{
    private readonly IContentManager _contentManager;
    private readonly IAccessDecision _decision;
    private readonly ICallerContextAccessor _callers;

    public ContentItemFilter(IContentManager contentManager, IAccessDecision decision, ICallerContextAccessor callers)
    {
        _contentManager = contentManager;
        _decision = decision;
        _callers = callers;
    }

    public async ValueTask<FluidValue> ProcessAsync(FluidValue input, FilterArguments arguments, LiquidTemplateContext ctx)
    {
        if (input.Type == FluidValues.Array)
        {
            // List of content item ids to return.
            var contentItemIds = input.Enumerate(ctx).Select(x => x.ToStringValue());
            var visible = new List<ContentItem>();
            foreach (var item in await _contentManager.GetAsync(contentItemIds))
            {
                if (await CanViewAsync(item))
                {
                    visible.Add(item);
                }
            }

            return FluidValue.Create(visible, ctx.Options);
        }

        var contentItem = await _contentManager.GetAsync(input.ToStringValue());
        return contentItem is not null && await CanViewAsync(contentItem)
            ? FluidValue.Create(contentItem, ctx.Options)
            : NilValue.Instance;
    }

    private Task<bool> CanViewAsync(ContentItem item) =>
        _decision.AllowsAsync(_callers.Current, CommonPermissions.ViewContent.Name, item);
}
