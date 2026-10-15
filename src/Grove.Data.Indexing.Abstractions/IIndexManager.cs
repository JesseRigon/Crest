
using Grove.Data.Indexing.Models;

namespace Grove.Data.Indexing;

public interface IIndexManager
{
    Task<bool> CreateAsync(IndexProfile indexProfile);

    Task<bool> RebuildAsync(IndexProfile indexProfile);

    Task<bool> DeleteAsync(IndexProfile indexProfile);

    Task<bool> ExistsAsync(string indexFullName);
}
