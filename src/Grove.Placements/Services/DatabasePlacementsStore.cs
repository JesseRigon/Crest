using Grove.Documents;
using Grove.Placements.Models;

namespace Grove.Placements.Services;

public class DatabasePlacementsStore : IPlacementStore
{
    private readonly IDocumentManager<PlacementsDocument> _documentManager;

    public DatabasePlacementsStore(IDocumentManager<PlacementsDocument> documentManager)
    {
        _documentManager = documentManager;
    }

    /// <inheritdoc />
    public Task<PlacementsDocument> LoadPlacementsAsync() => _documentManager.GetOrCreateMutableAsync();

    /// <inheritdoc />
    public Task<PlacementsDocument> GetPlacementsAsync() => _documentManager.GetOrCreateImmutableAsync();

    /// <inheritdoc />
    public Task SavePlacementsAsync(PlacementsDocument placementsDocument) => _documentManager.UpdateAsync(placementsDocument);
}
