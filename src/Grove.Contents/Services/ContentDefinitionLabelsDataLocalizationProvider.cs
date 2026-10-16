using Grove.Contents.Fields;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Management.Metadata.Settings;
using Grove.Data.Localization.Data;

namespace Grove.Contents.Services;

/// <summary>
/// The content definition labels beyond type names and field labels as localization sources
/// (docs/content-engine.md › Localization): field sets' names and descriptions, each attachment's
/// name on its type ("Billing", "Shipping") and each embedding's name. The editor's section titles
/// and labels are read through them.
/// </summary>
public sealed class ContentDefinitionLabelsDataLocalizationProvider(IContentDefinitionManager definitions) : ILocalizationDataProvider
{
    public async Task<IEnumerable<DataLocalizedString>> GetDescriptorsAsync()
    {
        var labels = new List<DataLocalizedString>();
        foreach (var fieldSet in await definitions.ListFieldSetDefinitionsAsync())
        {
            Add(labels, DataLocalizationContext.ContentFieldSet, fieldSet.DisplayName());
            Add(labels, DataLocalizationContext.ContentFieldSet, fieldSet.Description());
        }

        foreach (var type in await definitions.ListTypeDefinitionsAsync())
        {
            foreach (var attachment in type.FieldSets)
            {
                Add(labels, DataLocalizationContext.ContentFieldSetAttachment(type.Name), attachment.DisplayName());
            }

            foreach (var embedding in type.Embeddings)
            {
                Add(labels, DataLocalizationContext.ContentEmbedding(type.Name), embedding.DisplayNameOrName);
            }
        }

        return labels.DistinctBy(label => $"{label.Context}|{label.Name}");
    }

    private static void Add(List<DataLocalizedString> labels, string context, string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            labels.Add(new DataLocalizedString(context, text, string.Empty));
        }
    }
}
