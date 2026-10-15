using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.ContentFields.Fields;
using Grove.ContentManagement;
using Grove.ContentManagement.Metadata.Models;

namespace Grove.ContentFields.ViewModels;

public class EditContentPickerFieldViewModel
{
    public string ContentItemIds { get; set; }
    public ContentPickerField Field { get; set; }
    public ContentPart Part { get; set; }
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }

    [BindNever]
    public IList<VueMultiselectItemViewModel> SelectedItems { get; set; }
}
