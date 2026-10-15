using Grove.ContentFields.Fields;
using Grove.Contents.Indexing;
using Grove.Indexing;
using Grove.Modules;

namespace Grove.ContentFields.Indexing;

[RequireFeatures("Grove.ContentLocalization")]
public class LocalizationSetContentPickerFieldIndexHandler : ContentFieldIndexHandler<LocalizationSetContentPickerField>
{
    public override Task BuildIndexAsync(LocalizationSetContentPickerField field, BuildFieldIndexContext context)
    {
        var options = DocumentIndexOptions.Keyword | DocumentIndexOptions.Store;

        if (field.LocalizationSets.Length > 0)
        {
            foreach (var localizationSet in field.LocalizationSets)
            {
                foreach (var key in context.Keys)
                {
                    context.DocumentIndex.Set(key, localizationSet, options);
                }
            }
        }
        else
        {
            foreach (var key in context.Keys)
            {
                context.DocumentIndex.Set(key, ContentIndexingConstants.NullValue, options);
            }
        }

        return Task.CompletedTask;
    }
}
