using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Crest.Queries.ViewModels;

public class QueriesCreateViewModel
{
    public string SourceName { get; set; }

    [BindNever]
    public dynamic Editor { get; set; }
}
