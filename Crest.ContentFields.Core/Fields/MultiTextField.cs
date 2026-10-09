using Crest.ContentManagement;

namespace Crest.ContentFields.Fields;

public class MultiTextField : ContentField
{
    public string[] Values { get; set; } = [];
}
