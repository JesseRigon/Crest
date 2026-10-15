using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.ContentFields.Fields;
using Grove.ContentManagement;
using Grove.ContentManagement.Metadata.Models;

namespace Grove.ContentFields.ViewModels;

public class EditLocalizationSetContentPickerFieldViewModel
{
    public string LocalizationSets { get; set; }
    public LocalizationSetContentPickerField Field { get; set; }
    public ContentPart Part { get; set; }
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }

    [BindNever]
    public IList<VueMultiselectItemViewModel> SelectedItems { get; set; }
}
