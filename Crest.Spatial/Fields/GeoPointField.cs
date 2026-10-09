using Crest.ContentManagement;

namespace Crest.Spatial.Fields;

public class GeoPointField : ContentField
{
    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }
}
