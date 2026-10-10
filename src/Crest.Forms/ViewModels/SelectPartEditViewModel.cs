using Crest.Forms.Models;

namespace Crest.Forms.ViewModels;

public class SelectPartEditViewModel
{
    public string Options { get; set; }
    public string DefaultValue { get; set; }
    public SelectEditorOption Editor { get; set; }
}
