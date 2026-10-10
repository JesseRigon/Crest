using Crest.Documents;
using Crest.Placements.Models;

namespace Crest.Placements.Services;

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
