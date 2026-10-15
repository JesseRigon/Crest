using Grove.ContentManagement;
using Grove.ContentManagement.Metadata.Models;
using Grove.Spatial.Fields;

namespace Grove.Spatial.ViewModels;

public class DisplayGeoPointFieldViewModel
{
    public decimal? Latitude => Field?.Latitude;

    public decimal? Longitude => Field?.Longitude;

    public string FieldClass => PartFieldDefinition is null ? null : Grove.Mvc.Utilities.StringExtensions.HtmlClassify(PartFieldDefinition.PartDefinition.Name + "-" + PartFieldDefinition.Name);

    public GeoPointField Field { get; set; }
    public ContentPart Part { get; set; }
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }
}
