using Crest.ContentManagement;
using Crest.Modules;

namespace Crest.ContentFields.Fields;

[RequireFeatures("Crest.ContentLocalization")]
public class LocalizationSetContentPickerField : ContentField
{
    public string[] LocalizationSets { get; set; } = [];
}
