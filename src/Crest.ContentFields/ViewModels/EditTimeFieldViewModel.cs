using Crest.ContentFields.Fields;
using Crest.ContentManagement;
using Crest.ContentManagement.Metadata.Models;

namespace Crest.ContentFields.ViewModels;

public class EditTimeFieldViewModel
{
    public TimeSpan? Value { get; set; }
    public TimeField Field { get; set; }
    public ContentPart Part { get; set; }
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }
}
