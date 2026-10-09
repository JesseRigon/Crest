using Crest.ContentManagement;

namespace Crest.Forms.Models;

public sealed class FormInputElementVisibilityPart : ContentPart
{
    public IEnumerable<FormVisibilityRuleGroup> Groups { get; set; }

    public FormVisibilityAction Action { get; set; } = FormVisibilityAction.None;
}
