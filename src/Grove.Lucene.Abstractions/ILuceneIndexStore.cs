using Lucene.Net.Index;
using Lucene.Net.Search;
using Grove.Indexing.Models;

namespace Grove.Lucene;

public interface ILuceneIndexStore
{
    Task<bool> ExistsAsync(string indexFullName);

    Task<bool> RemoveAsync(IndexProfile index);

    Task SearchAsync(IndexProfile index, Func<IndexSearcher, Task> searcher);

    Task WriteAndClose(IndexProfile index);

    Task WriteAsync(IndexProfile index, Action<IndexWriter> action);
}
