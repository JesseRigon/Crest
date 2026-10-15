using Grove.Contents.Management.Metadata.Settings;

namespace Grove.Data.Types.Spatial.Settings;

public class GeoPointFieldSettings : FieldSettings
{
    /// <summary>
    /// A Google Maps browser key: the map editor shows Google Maps with it; without one it shows
    /// OpenStreetMap through Leaflet, which needs no key.
    /// </summary>
    public string GoogleMapsApiKey { get; set; }
}
