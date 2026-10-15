using Grove.Contents.Management;

namespace Grove.Data.Types.Spatial.Fields;

public class GeoPointField : ContentField
{
    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }
}
