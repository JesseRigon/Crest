using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.ContentManagement;
using Grove.ContentManagement.Metadata.Models;
using Grove.Spatial.Fields;

namespace Grove.Spatial.ViewModels;

public class EditGeoPointFieldViewModel
{
    public string Latitude { get; set; }
    public string Longitude { get; set; }

    [BindNever]
    public GeoPointField Field { get; set; }

    [BindNever]
    public ContentPart Part { get; set; }

    [BindNever]
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }
}
