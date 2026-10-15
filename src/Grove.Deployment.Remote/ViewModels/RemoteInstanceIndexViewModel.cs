using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.Deployment.Remote.Models;

namespace Grove.Deployment.Remote.ViewModels;

public class RemoteInstanceIndexViewModel
{
    public List<RemoteInstance> RemoteInstances { get; set; }

    public ContentOptions Options { get; set; } = new ContentOptions();

    [BindNever]
    public dynamic Pager { get; set; }
}
