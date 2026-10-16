using System.Text.Json.Nodes;
using Fluid;
using Fluid.Values;
using Grove.Contents.Management;
using Grove.Display.Liquid;
using Grove.Fields;

namespace Grove.Contents.Taxonomies.Liquid;

/// <summary>
/// <c>taxonomy_terms</c>: the terms an option list field on a taxonomy's terms holds (the field, or
/// its stored JSON), in the field's order; or the terms of a list of ids in the taxonomy named by
/// the argument.
/// </summary>
public class TaxonomyTermsFilter(IContentManager contentManager, IEmbeddedContentItemResolver embeddedItems) : ILiquidFilter
{
    public async ValueTask<FluidValue> ProcessAsync(FluidValue input, FilterArguments arguments, LiquidTemplateContext ctx)
    {
        string[] termContentItemIds;
        if (input.Type == FluidValues.Object && input.ToObjectValue() is OptionListField field)
        {
            termContentItemIds = field.SelectedIds ?? [];
        }
        else if (input.Type == FluidValues.Object
            && input.ToObjectValue() is JsonObject json
            && json[nameof(OptionListField.SelectedIds)] is JsonArray selected)
        {
            termContentItemIds = [.. selected.Select(id => id?.GetValue<string>()).Where(id => !string.IsNullOrEmpty(id))];
        }
        else if (input.Type == FluidValues.Array)
        {
            var taxonomy = await contentManager.GetAsync(arguments.At(0).ToStringValue());
            if (taxonomy is null)
            {
                return NilValue.Instance;
            }

            var ids = input.Enumerate(ctx).Select(x => x.ToStringValue());
            return FluidValue.Create(ids.Select(id => TaxonomyTerms.FindTerm(TaxonomyTerms.Roots(taxonomy), id)).Where(term => term is not null).ToList(), ctx.Options);
        }
        else
        {
            return NilValue.Instance;
        }

        // Each term from the taxonomy (its published version) that holds it.
        var terms = new List<ContentItem>();
        foreach (var id in termContentItemIds)
        {
            if (await embeddedItems.ResolveAsync(id) is { Embedding: Models.TaxonomyFieldSet.TermsEmbedding } reference)
            {
                terms.Add(reference.Item);
            }
        }

        return FluidValue.Create(terms, ctx.Options);
    }
}
