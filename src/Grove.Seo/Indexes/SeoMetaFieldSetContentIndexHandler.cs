using Grove.Contents.Management;
using Grove.Data.Indexing;
using Grove.Seo.Models;

namespace Grove.Seo.Indexes;

public class SeoMetaFieldSetContentIndexHandler : IDocumentIndexHandler
{
    public const string PageTitleKey = "Content.ContentItem.SeoMeta.PageTitle";
    public const string MetaDescriptionKey = "Content.ContentItem.SeoMeta.MetaDescription";
    public const string MetaKeywordsKey = "Content.ContentItem.SeoMeta.MetaKeywords";

    public Task BuildIndexAsync(BuildDocumentIndexContext context)
    {
        if (context.Record is not ContentItem contentItem)
        {
            return Task.CompletedTask;
        }

        if (!contentItem.TryGet<SeoMetaFieldSet>(out var parent))
        {
            return Task.CompletedTask;
        }

        context.DocumentIndex.Set(
            PageTitleKey,
            parent.PageTitle,
            DocumentIndexOptions.Store);

        context.DocumentIndex.Set(
            MetaDescriptionKey,
            parent.MetaDescription,
            DocumentIndexOptions.Store);

        context.DocumentIndex.Set(
            MetaKeywordsKey,
            parent.MetaKeywords,
            DocumentIndexOptions.Store);

        return Task.CompletedTask;
    }
}
