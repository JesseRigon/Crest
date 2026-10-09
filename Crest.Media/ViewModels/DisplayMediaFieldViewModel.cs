using Crest.ContentManagement;
using Crest.ContentManagement.Metadata.Models;
using Crest.Media.Fields;

namespace Crest.Media.ViewModels;

public class DisplayMediaFieldViewModel
{
    public string[] Paths => Field.Paths;
    public MediaField Field { get; set; }
    public ContentPart Part { get; set; }
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }
}
