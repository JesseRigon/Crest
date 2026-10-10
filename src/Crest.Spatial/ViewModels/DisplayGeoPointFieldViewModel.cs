using Crest.ContentManagement;
using Crest.ContentManagement.Metadata.Models;
using Crest.Spatial.Fields;

namespace Crest.Spatial.ViewModels;

public class DisplayGeoPointFieldViewModel
{
    public GeoPointField Field { get; set; }
    public ContentPart Part { get; set; }
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }
}
