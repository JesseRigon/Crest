using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Crest.Lucene.Settings;

public class ContentPickerFieldLuceneEditorSettings
{
    public string Index { get; set; }

    [BindNever]
    public string[] Indices { get; set; }
}
