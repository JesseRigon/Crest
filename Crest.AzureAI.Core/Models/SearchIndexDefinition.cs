using Crest.Indexing.Models;
using static Crest.Indexing.DocumentIndex;

namespace Crest.AzureAI.Models;

public sealed class SearchIndexDefinition
{
    public SearchIndexDefinition(
        AzureAISearchIndexMap indexMap,
        DocumentIndexEntry indexEntry,
        IndexProfile index)
    {
        Map = indexMap;
        IndexEntry = indexEntry;
        IndexProfile = index;
    }

    public AzureAISearchIndexMap Map { get; }

    public DocumentIndexEntry IndexEntry { get; }

    public IndexProfile IndexProfile { get; }

    public bool IsRootField { get; set; }
}
