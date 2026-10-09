using Crest.Data.Documents;

namespace Crest.Deployment.Remote.Models;

public class RemoteInstanceList : Document
{
    public List<RemoteInstance> RemoteInstances { get; set; } = [];
}
