using Grove.Data.Documents;

namespace Grove.Deployment.Remote.Models;

public class RemoteInstanceList : Document
{
    public List<RemoteInstance> RemoteInstances { get; set; } = [];
}
