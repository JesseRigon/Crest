using Grove.Contents.Management;
using Grove.Contents.Management.Metadata.Models;
using Grove.Data.Types.Spatial.Fields;

namespace Grove.Data.Types.Spatial.ViewModels;

public class DisplayGeoPointFieldViewModel
{
    public decimal? Latitude => Field?.Latitude;

    public decimal? Longitude => Field?.Longitude;

    public string FieldClass => PartFieldDefinition is null ? null : Grove.Utilities.StringExtensions.HtmlClassify(PartFieldDefinition.PartDefinition.Name + "-" + PartFieldDefinition.Name);

    public GeoPointField Field { get; set; }
    public ContentPart Part { get; set; }
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }
}
