using Crest.ContentFields.Fields;
using Crest.ContentManagement;
using Crest.ContentManagement.Metadata.Models;

namespace Crest.ContentFields.ViewModels;

public class DisplayDateTimeFieldViewModel
{
    public DateTime? Value => Field.Value;
    public DateTime? LocalDateTime { get; set; }
    public DateTimeField Field { get; set; }
    public ContentPart Part { get; set; }
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }
}
