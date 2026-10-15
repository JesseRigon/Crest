using Grove.Display.Layers.Models;

namespace Grove.Display.Layers.Services;

public interface ILayerService
{
    /// <summary>Loads the layers document from the store for updating; never cached.</summary>
    Task<LayersDocument> LoadLayersAsync();

    /// <summary>Gets the layers document from the cache for reading; never updated.</summary>
    Task<LayersDocument> GetLayersAsync();

    /// <summary>Stores the layers and the placements and refreshes the cache.</summary>
    Task UpdateAsync(LayersDocument layers);
}
