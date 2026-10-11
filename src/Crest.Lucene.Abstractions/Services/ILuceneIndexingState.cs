
namespace Crest.Lucene;

public interface ILuceneIndexingState
{
    Task<long> GetLastTaskIdAsync(string indexFullName);

    Task SetLastTaskIdAsync(string indexFullName, long taskId);
}
