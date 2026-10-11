using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Crest.Elasticsearch.Models;

public class ContentPickerFieldElasticEditorSettings
{
    public string Index { get; set; }

    [BindNever]
    public string[] Indices { get; set; }
}
