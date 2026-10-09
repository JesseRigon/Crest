using Crest.ContentFields.Fields;
using Crest.ContentManagement;
using Crest.ContentManagement.Metadata.Models;

namespace Crest.ContentFields.ViewModels;

public class DisplayLocalizationSetContentPickerFieldViewModel
{
    public string[] LocalizationSets => Field.LocalizationSets;
    public LocalizationSetContentPickerField Field { get; set; }
    public ContentPart Part { get; set; }
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }
}
