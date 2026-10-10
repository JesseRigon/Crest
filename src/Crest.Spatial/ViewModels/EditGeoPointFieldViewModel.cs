using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.ContentManagement;
using Crest.ContentManagement.Metadata.Models;
using Crest.Spatial.Fields;

namespace Crest.Spatial.ViewModels;

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
