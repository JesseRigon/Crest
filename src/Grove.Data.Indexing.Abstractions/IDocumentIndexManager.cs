
using Grove.Data.Indexing.Models;

namespace Grove.Data.Indexing;

public interface IDocumentIndexManager
{
    Task<bool> AddOrUpdateDocumentsAsync(IndexProfile indexProfile, IEnumerable<DocumentIndex> documents);

    Task<bool> DeleteDocumentsAsync(IndexProfile indexProfile, IEnumerable<string> documentIds);

    Task<bool> DeleteAllDocumentsAsync(IndexProfile indexProfile);

    Task<long> GetLastTaskIdAsync(IndexProfile indexProfile);

    Task SetLastTaskIdAsync(IndexProfile indexProfile, long lastTaskId);

    IContentIndexSettings GetContentIndexSettings();
}
