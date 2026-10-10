using System.ComponentModel;
using Crest.ContentManagement;

namespace Crest.Forms.Models;

public class FormPart : ContentPart
{
    public string Action { get; set; }

    public string Method { get; set; }

    public string WorkflowTypeId { get; set; }

    public string EncType { get; set; }

    [DefaultValue(true)]
    public bool EnableAntiForgeryToken { get; set; } = true;

    [DefaultValue(true)]
    public bool SaveFormLocation { get; set; } = true;
}
