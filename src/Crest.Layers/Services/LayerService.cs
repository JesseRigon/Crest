using Crest.Documents;
using Crest.Layers.Models;

namespace Crest.Layers.Services;

public sealed class LayerService(IDocumentManager<LayersDocument> documentManager) : ILayerService
{
    public Task<LayersDocument> LoadLayersAsync() => documentManager.GetOrCreateMutableAsync();

    public Task<LayersDocument> GetLayersAsync() => documentManager.GetOrCreateImmutableAsync();

    public async Task UpdateAsync(LayersDocument layers)
    {
        // The loaded, tracked document is the one stored (passing the argument stored a detached copy).
        var existing = await LoadLayersAsync();
        existing.Layers = layers.Layers;
        existing.Placements = layers.Placements;
        await documentManager.UpdateAsync(existing);
    }
}
