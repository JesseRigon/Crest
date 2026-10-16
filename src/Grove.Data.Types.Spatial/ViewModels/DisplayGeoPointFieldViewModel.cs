using Grove.Contents.Management;
using Grove.Contents.Management.Metadata.Models;
using Grove.Data.Types.Spatial.Fields;

namespace Grove.Data.Types.Spatial.ViewModels;

public class DisplayGeoPointFieldViewModel
{
    public decimal? Latitude => Field?.Latitude;

    public decimal? Longitude => Field?.Longitude;

    public string FieldClass => FieldDefinition is null ? null : Grove.Utilities.StringExtensions.HtmlClassify(FieldDefinition.FieldSetDefinition.Name + "-" + FieldDefinition.Name);

    public GeoPointField Field { get; set; }
    public ContentFieldSet FieldSet { get; set; }
    public ContentFieldDefinition FieldDefinition { get; set; }
}
