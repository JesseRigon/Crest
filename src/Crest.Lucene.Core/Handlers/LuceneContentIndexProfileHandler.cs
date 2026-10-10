using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Crest.ContentManagement;
using Crest.Contents.Indexing;
using Crest.Entities;
using Crest.Indexing;
using Crest.Indexing.Core;
using Crest.Indexing.Core.Handlers;
using Crest.Indexing.Core.Models;
using Crest.Indexing.Models;
using Crest.Infrastructure.Entities;
using Crest.Modules;
using Crest.Lucene.Model;
using Crest.Lucene.Models;

namespace Crest.Lucene.Core.Handlers;

public sealed class LuceneContentIndexProfileHandler : IndexProfileHandlerBase
{
    private readonly IContentManager _contentManager;
    private readonly IEnumerable<IDocumentIndexHandler> _contentItemIndexHandlers;
    private readonly ILogger _logger;

    public LuceneContentIndexProfileHandler(
        IContentManager contentManager,
        IEnumerable<IDocumentIndexHandler> contentItemIndexHandlers,
        ILogger<LuceneContentIndexProfileHandler> logger)
    {
        _contentManager = contentManager;
        _contentItemIndexHandlers = contentItemIndexHandlers;
        _logger = logger;
    }

    public override Task InitializingAsync(InitializingContext<IndexProfile> context)
       => PopulateAsync(context.Model, context.Data);

    public override Task CreatingAsync(CreatingContext<IndexProfile> context)
        => SetMappingsAsync(context.Model);

    public override Task UpdatingAsync(UpdatingContext<IndexProfile> context)
        => SetMappingsAsync(context.Model);

    private async Task SetMappingsAsync(IndexProfile index)
    {
        if (!CanHandle(index))
        {
            return;
        }

        var LuceneMetadata = index.GetOrCreate<LuceneIndexMetadata>();

        var map = new LuceneIndexMap()
        {
            KeyFieldName = ContentIndexingConstants.ContentItemIdKey,
        };

        var metadata = index.GetOrCreate<ContentIndexMetadata>();

        map.Fields = (await PopulateTypeMappingAsync(metadata)).ToArray();

        LuceneMetadata.IndexMappings = map;

        index.Put(LuceneMetadata);
    }

    private async Task PopulateAsync(IndexProfile index, JsonNode data)
    {
        if (!CanHandle(index))
        {
            return;
        }

        var LuceneMetadata = index.GetOrCreate<LuceneIndexMetadata>();

        var map = new LuceneIndexMap()
        {
            KeyFieldName = ContentIndexingConstants.ContentItemIdKey,
        };

        var metadata = index.GetOrCreate<ContentIndexMetadata>();

        map.Fields = (await PopulateTypeMappingAsync(metadata)).ToArray();

        LuceneMetadata.IndexMappings = map;

        var analyzerName = data[nameof(LuceneMetadata.AnalyzerName)]?.GetValue<string>();

        if (!string.IsNullOrEmpty(analyzerName))
        {
            LuceneMetadata.AnalyzerName = analyzerName;
        }

        var storeSourceData = data[nameof(LuceneMetadata.StoreSourceData)]?.GetValue<bool>();

        if (storeSourceData.HasValue)
        {
            LuceneMetadata.StoreSourceData = storeSourceData.Value;
        }

        index.Put(LuceneMetadata);

        var queryMetadata = index.GetOrCreate<LuceneIndexDefaultQueryMetadata>();

        if (queryMetadata.DefaultSearchFields is null || queryMetadata.DefaultSearchFields.Length == 0)
        {
            queryMetadata.DefaultSearchFields = [ContentIndexingConstants.FullTextKey];
        }
    }

    private static bool CanHandle(IndexProfile index)
    {
        return string.Equals(LuceneConstants.ProviderName, index.ProviderName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(IndexingConstants.ContentsIndexSource, index.Type, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<HashSet<string>> PopulateTypeMappingAsync(ContentIndexMetadata metadata)
    {
        var fields = new HashSet<string>()
        {
            ContentIndexingConstants.ContentItemIdKey,
            ContentIndexingConstants.ContentItemVersionIdKey,
            ContentIndexingConstants.FullTextKey,
        };

        if (metadata.IndexedContentTypes is null || metadata.IndexedContentTypes.Length == 0)
        {
            return fields;
        }

        foreach (var contentType in metadata.IndexedContentTypes)
        {
            var contentItem = await _contentManager.NewAsync(contentType);

            var document = new ContentItemDocumentIndex(contentItem.ContentItemId, contentItem.ContentItemVersionId);
            var buildIndexContext = new BuildDocumentIndexContext(document, contentItem, [contentType], new LuceneContentIndexSettings());

            await _contentItemIndexHandlers.InvokeAsync(x => x.BuildIndexAsync(buildIndexContext), _logger);

            foreach (var entry in document.Entries)
            {
                if (entry.Name == ContentIndexingConstants.ContentItemIdKey || entry.Name == ContentIndexingConstants.ContentItemVersionIdKey)
                {
                    continue;
                }

                fields.Add(entry.Name);
            }
        }

        return fields;
    }
}
