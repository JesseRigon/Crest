using Crest.ContentManagement;

namespace Crest.Forms.Models;

/// <summary>
/// Turns a content item into a form element that supports input.
/// </summary>
public class FormInputElementPart : ContentPart
{
    public string Name { get; set; }
}
