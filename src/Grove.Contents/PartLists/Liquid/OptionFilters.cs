using Grove.Display.Liquid;
using Grove.Models;
using Grove.Services;
using Fluid;
using Fluid.Values;

namespace Grove.Contents.PartLists.Liquid;

// Liquid over the same service the content part lists API wraps (docs/content-engine.md ›
// Liquid over content). No authorization check: a list's
// options are labels, not data, and a template that needs a permission says so explicitly
// (has_permission).

/// <summary>
/// <c>{{ "order-status" | option_list }}</c>: the options an editor would offer for a list
/// (ordered, hidden ones dropped). Empty when the list does not exist.
/// </summary>
public sealed class OptionListFilter(IContentPartListService lists) : ILiquidFilter
{
    public async ValueTask<FluidValue> ProcessAsync(FluidValue input, FilterArguments arguments, LiquidTemplateContext ctx)
    {
        var key = input.ToStringValue();
        if (string.IsNullOrWhiteSpace(key))
        {
            return EmptyValue.Instance;
        }

        return FluidValue.Create(await lists.GetSelectableAsync(key), ctx.Options);
    }
}

/// <summary>
/// <c>{{ item.Content.Order.Status | option_label }}</c>: the label a human reads for a stored
/// option. The input is what content stores - an option's ContentItemId, a list of them, or a
/// taxonomy-style field with <c>TermContentItemIds</c>. With a list key
/// (<c>option_label: "order-status"</c>) an option key is accepted too. A list of values
/// gives a list of labels; an unknown value gives nil, never the opaque id.
/// </summary>
public sealed class OptionLabelFilter(IContentPartListService lists) : ILiquidFilter
{
    public async ValueTask<FluidValue> ProcessAsync(FluidValue input, FilterArguments arguments, LiquidTemplateContext ctx)
    {
        var values = await ValuesAsync(input, ctx);
        if (values.Count == 0)
        {
            return NilValue.Instance;
        }

        var listKey = arguments.At(0).ToStringValue();
        var options = string.IsNullOrWhiteSpace(listKey)
            ? (await lists.ListAsync()).SelectMany(list => list.Options).ToList()
            : (await lists.GetAsync(listKey))?.Options.ToList() ?? [];

        var labels = values.Select(value => Label(options, value, byKey: !string.IsNullOrWhiteSpace(listKey))).ToList();

        return input.Type == FluidValues.Array || labels.Count > 1
            ? new ArrayValue(labels.Select(label => label is null ? (FluidValue)NilValue.Instance : new StringValue(label)).ToArray())
            : labels[0] is { } single ? new StringValue(single) : NilValue.Instance;
    }

    public static string? Label(IEnumerable<GroveOptionModel> options, string value, bool byKey)
    {
        var option = options.FirstOrDefault(option => string.Equals(option.ContentItemId, value, StringComparison.OrdinalIgnoreCase))
            ?? (byKey ? options.FirstOrDefault(option => string.Equals(option.Key, value, StringComparison.OrdinalIgnoreCase)) : null);

        return option?.DisplayText;
    }

    private static async ValueTask<List<string>> ValuesAsync(FluidValue input, TemplateContext ctx)
    {
        if (input.Type == FluidValues.Array)
        {
            return [.. input.Enumerate(ctx).Select(value => value.ToStringValue()).Where(value => !string.IsNullOrWhiteSpace(value))];
        }

        if (input.Type == FluidValues.Object)
        {
            var ids = await input.GetValueAsync("TermContentItemIds", ctx);
            return ids.Type == FluidValues.Array ? await ValuesAsync(ids, ctx) : [];
        }

        var single = input.ToStringValue();
        return string.IsNullOrWhiteSpace(single) ? [] : [single];
    }
}
