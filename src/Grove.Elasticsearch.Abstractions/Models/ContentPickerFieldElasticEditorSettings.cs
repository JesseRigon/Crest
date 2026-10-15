using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Grove.Elasticsearch.Models;

public class ContentPickerFieldElasticEditorSettings
{
    public string Index { get; set; }

    [BindNever]
    public string[] Indices { get; set; }
}
