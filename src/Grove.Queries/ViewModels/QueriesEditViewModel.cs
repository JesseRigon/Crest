using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Grove.Queries.ViewModels;

public class QueriesEditViewModel
{
    public string QueryId { get; set; }

    [BindNever]
    public string Name { get; set; }

    [BindNever]
    public dynamic Editor { get; set; }
}
