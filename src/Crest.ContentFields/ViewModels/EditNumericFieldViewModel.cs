using Crest.ContentFields.Fields;
using Crest.ContentManagement;
using Crest.ContentManagement.Metadata.Models;

namespace Crest.ContentFields.ViewModels;

public class EditNumericFieldViewModel
{
    public string Value { get; set; }
    public NumericField Field { get; set; }
    public ContentPart Part { get; set; }
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }
}
