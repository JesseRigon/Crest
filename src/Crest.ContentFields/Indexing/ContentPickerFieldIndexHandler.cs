using Crest.ContentFields.Fields;
using Crest.Contents.Indexing;
using Crest.Indexing;

namespace Crest.ContentFields.Indexing;

public class ContentPickerFieldIndexHandler : ContentFieldIndexHandler<ContentPickerField>
{
    public override Task BuildIndexAsync(ContentPickerField field, BuildFieldIndexContext context)
    {
        var options = DocumentIndexOptions.Keyword | DocumentIndexOptions.Store;

        if (field.ContentItemIds.Length > 0)
        {
            foreach (var contentItemId in field.ContentItemIds)
            {
                foreach (var key in context.Keys)
                {
                    context.DocumentIndex.Set(key, contentItemId, options);
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
