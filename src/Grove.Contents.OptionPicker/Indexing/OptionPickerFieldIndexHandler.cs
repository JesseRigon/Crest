using Grove.Contents.Indexing;
using Grove.Fields;

namespace Grove.Data.Indexing;

/// <summary>Search indexing of an option picker field: its selected ids, as keywords.</summary>
public sealed class OptionPickerFieldIndexHandler : ContentFieldIndexHandler<OptionPickerField>
{
    public override Task BuildIndexAsync(OptionPickerField field, BuildFieldIndexContext context)
    {
        var options = DocumentIndexOptions.Keyword | DocumentIndexOptions.Store;

        if (field.SelectedIds.Length > 0)
        {
            foreach (var id in field.SelectedIds)
            {
                foreach (var key in context.Keys)
                {
                    context.DocumentIndex.Set(key, id, options);
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
