using Crest.ContentManagement;
using Crest.Entities;
using Crest.Indexing.Handlers;
using Crest.Indexing.Models;
using Crest.Infrastructure.Entities;
using Crest.Lucene.Models;

namespace Crest.Lucene.Handlers;

public sealed class LuceneIndexProfileHandler : IndexProfileHandlerBase
{
    public override Task InitializingAsync(InitializingContext<IndexProfile> context)
    {
        if (!CanHandle(context.Model))
        {
            return Task.CompletedTask;
        }

        var LuceneMetadata = context.Model.GetOrCreate<LuceneIndexMetadata>();

        var analyzerName = context.Data[nameof(LuceneMetadata.AnalyzerName)]?.GetValue<string>();

        if (!string.IsNullOrEmpty(analyzerName))
        {
            LuceneMetadata.AnalyzerName = analyzerName;
        }

        var storeSourceData = context.Data[nameof(LuceneMetadata.StoreSourceData)]?.GetValue<bool>();

        if (storeSourceData.HasValue)
        {
            LuceneMetadata.StoreSourceData = storeSourceData.Value;
        }

        context.Model.Put(LuceneMetadata);

        return Task.CompletedTask;
    }

    private static bool CanHandle(IndexProfile index)
    {
        return string.Equals(LuceneConstants.ProviderName, index.ProviderName, StringComparison.OrdinalIgnoreCase);
    }
}
