using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.Deployment.Remote.Models;

namespace Crest.Deployment.Remote.ViewModels;

public class RemoteInstanceIndexViewModel
{
    public List<RemoteInstance> RemoteInstances { get; set; }

    public ContentOptions Options { get; set; } = new ContentOptions();

    [BindNever]
    public dynamic Pager { get; set; }
}
