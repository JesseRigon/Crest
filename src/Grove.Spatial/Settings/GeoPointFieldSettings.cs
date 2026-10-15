using Grove.ContentManagement.Metadata.Settings;

namespace Grove.Spatial.Settings;

public class GeoPointFieldSettings : FieldSettings
{
    /// <summary>
    /// A Google Maps browser key: the map editor shows Google Maps with it; without one it shows
    /// OpenStreetMap through Leaflet, which needs no key.
    /// </summary>
    public string GoogleMapsApiKey { get; set; }
}
