using Grove.ContentManagement;
using Grove.ContentManagement.Models;
using Grove.Indexing;
using Grove.Mvc.Utilities;

namespace Grove.Contents.Indexing;

public class AspectsContentIndexHandler : IDocumentIndexHandler
{
    private readonly IContentManager _contentManager;

    public AspectsContentIndexHandler(IContentManager contentManager)
    {
        _contentManager = contentManager;
    }

    public async Task BuildIndexAsync(BuildDocumentIndexContext context)
    {
        if (context.Record is not ContentItem contentItem)
        {
            return;
        }

        var body = await _contentManager.PopulateAspectAsync(contentItem, new BodyAspect());

        if (body != null && body.Body != null)
        {
            context.DocumentIndex.Set(
                ContentIndexingConstants.BodyAspectBodyKey,
                body.Body,
                DocumentIndexOptions.Sanitize);
        }

        context.DocumentIndex.Set(
            ContentIndexingConstants.DisplayTextAnalyzedKey,
            contentItem.DisplayText,
            DocumentIndexOptions.Sanitize);

        // Stored, so a query can return the value without loading the item
        context.DocumentIndex.Set(
            ContentIndexingConstants.DisplayTextKey + ContentIndexingConstants.KeywordKey,
            contentItem.DisplayText,
            DocumentIndexOptions.Keyword | DocumentIndexOptions.Store);

        // Stored, so a query can return the value without loading the item
        context.DocumentIndex.Set(
            ContentIndexingConstants.DisplayTextNormalizedKey,
            contentItem.DisplayText?.ReplaceDiacritics().ToLower(),
            DocumentIndexOptions.Keyword | DocumentIndexOptions.Store);
    }
}
