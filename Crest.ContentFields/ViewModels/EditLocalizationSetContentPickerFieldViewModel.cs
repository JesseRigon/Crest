using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.ContentFields.Fields;
using Crest.ContentManagement;
using Crest.ContentManagement.Metadata.Models;

namespace Crest.ContentFields.ViewModels;

public class EditLocalizationSetContentPickerFieldViewModel
{
    public string LocalizationSets { get; set; }
    public LocalizationSetContentPickerField Field { get; set; }
    public ContentPart Part { get; set; }
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }

    [BindNever]
    public IList<VueMultiselectItemViewModel> SelectedItems { get; set; }
}
