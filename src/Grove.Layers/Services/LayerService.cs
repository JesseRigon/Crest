using Grove.Documents;
using Grove.Layers.Models;

namespace Grove.Layers.Services;

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

/// <summary>The layers' part of a tree's composite version (decision 12): the layers document, rules and placements.</summary>
public sealed class LayersTreeVersion(ILayerService layers) : Grove.DisplayManagement.Components.IDisplayTreeVersionPart
{
    public async ValueTask<string?> GetVersionAsync(CancellationToken cancellationToken = default) =>
        (await layers.GetLayersAsync()).Identifier;
}
