using Grove.Data.Documents;
using Grove.Documents;
using Grove.Display.Placements.Models;

namespace Grove.Display.Placements.Services;

public class FilePlacementsStore : IPlacementStore
{
    private readonly IDocumentManager<IFileDocumentStore, PlacementsDocument> _documentManager;

    public FilePlacementsStore(IDocumentManager<IFileDocumentStore, PlacementsDocument> documentManager)
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
