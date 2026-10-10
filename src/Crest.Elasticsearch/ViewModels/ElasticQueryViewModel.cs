using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Crest.Elasticsearch.ViewModels;

public class ElasticQueryViewModel
{
    public string Index { get; set; }

    public string Query { get; set; }

    public bool ReturnContentItems { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Indexes { get; set; }
}
