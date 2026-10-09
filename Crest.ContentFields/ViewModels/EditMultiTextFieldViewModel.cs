using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.ContentFields.Fields;
using Crest.ContentManagement;
using Crest.ContentManagement.Metadata.Models;

namespace Crest.ContentFields.ViewModels;

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
