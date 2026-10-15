using Grove.ContentFields.Fields;
using Grove.ContentManagement;
using Grove.ContentManagement.Metadata.Models;

namespace Grove.ContentFields.ViewModels;

public class EditDateFieldViewModel
{
    public DateTime? Value { get; set; }
    public DateField Field { get; set; }
    public ContentPart Part { get; set; }
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }
}
