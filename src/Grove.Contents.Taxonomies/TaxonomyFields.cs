using Grove.Contents.Management;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Taxonomies.Models;
using Grove.Fields;
using Grove.Infrastructure.Settings;
using Grove.Models;

namespace Grove.Contents.Taxonomies;

/// <summary>
/// Filing content under a taxonomy is an option list field on the taxonomy's terms
/// (docs/content-engine.md › Picking is one field type): its source is
/// <c>embedded:Taxonomy.Terms</c> and a static filter on <c>Parent</c> names the taxonomy. Its
/// selections are rows of the option list field index (<c>SelectedId</c> the term), which the term
/// pages and the content list's filters query.
/// </summary>
public static class TaxonomyFields
{
    /// <summary>The source of a taxonomy's terms.</summary>
    public static readonly string SourceKey = OptionSourceKeys.ForEmbedding("Taxonomy", TaxonomyFieldSet.TermsEmbedding);

    /// <summary>The settings of an option list field on a taxonomy's terms.</summary>
    public static OptionListFieldSettings Settings(string taxonomyContentItemId, bool multiple = true, bool leavesOnly = false, bool allowCreate = false) => new()
    {
        SourceKey = SourceKey,
        Multiple = multiple,
        AllowCreate = allowCreate,
        Filters =
        [
            new OptionFilter { Path = "Parent", Value = taxonomyContentItemId },
            .. leavesOnly ? [new OptionFilter { Path = "Leaf", Value = "true" }] : Array.Empty<OptionFilter>(),
        ],
    };

    /// <summary>The taxonomy an option list field picks terms of; null for any other field.</summary>
    public static string TaxonomyOf(ContentFieldDefinition field)
    {
        if (field.FieldTypeDefinition.Name != nameof(OptionListField))
        {
            return null;
        }

        var settings = field.GetSettings<OptionListFieldSettings>();
        return string.Equals(settings.SourceKey, SourceKey, StringComparison.OrdinalIgnoreCase)
            ? settings.Filters.FirstOrDefault(filter => !filter.IsDependent && string.Equals(filter.Path, "Parent", StringComparison.OrdinalIgnoreCase))?.Value
            : null;
    }

    /// <summary>Every term of a taxonomy, at any depth.</summary>
    public static IEnumerable<ContentItem> AllTerms(ContentItem taxonomy)
    {
        foreach (var term in TaxonomyFieldSet.Terms(taxonomy))
        {
            yield return term;
            foreach (var descendant in AllTerms(term))
            {
                yield return descendant;
            }
        }
    }
}
