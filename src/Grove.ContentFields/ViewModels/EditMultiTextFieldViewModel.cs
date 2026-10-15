using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.ContentFields.Fields;
using Grove.ContentManagement;
using Grove.ContentManagement.Metadata.Models;

namespace Grove.ContentFields.ViewModels;

public class EditMultiTextFieldViewModel
{
    public string[] Values { get; set; } = [];

    [BindNever]
    public MultiTextField Field { get; set; }

    [BindNever]
    public ContentPart Part { get; set; }

    [BindNever]
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }
}
