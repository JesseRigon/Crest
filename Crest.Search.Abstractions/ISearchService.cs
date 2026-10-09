using Crest.Indexing.Models;

namespace Crest.Search.Abstractions;

public interface ISearchService
{
    string Name { get; }

    Task<SearchResult> SearchAsync(IndexProfile index, string term, int start, int size);
}
