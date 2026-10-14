using Crest.ContentManagement;
using Crest.ContentManagement.Metadata.Models;
using Crest.Spatial.Fields;

namespace Crest.Spatial.ViewModels;

public class DisplayGeoPointFieldViewModel
{
    public decimal? Latitude => Field?.Latitude;

    public decimal? Longitude => Field?.Longitude;

    public string FieldClass => PartFieldDefinition is null ? null : Crest.Mvc.Utilities.StringExtensions.HtmlClassify(PartFieldDefinition.PartDefinition.Name + "-" + PartFieldDefinition.Name);

    public GeoPointField Field { get; set; }
    public ContentPart Part { get; set; }
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }
}
