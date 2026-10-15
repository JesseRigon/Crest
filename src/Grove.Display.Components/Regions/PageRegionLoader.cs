namespace Grove.Display.Components.Regions;

/// <summary>
/// Where module client libraries load on demand (the admin WASM client), the assemblies that
/// contribute to page regions may not be loaded yet: a region awaits this before it renders.
/// Absent (server rendering, where every assembly is loaded), regions render at once.
/// </summary>
public interface IPageRegionContributorLoader
{
    /// <summary>Loads every module assembly with region contributors; the component registry picks them up.</summary>
    Task EnsureLoadedAsync();
}
