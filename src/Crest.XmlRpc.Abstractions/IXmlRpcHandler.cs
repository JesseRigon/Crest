using System.Xml.Linq;

namespace Crest.XmlRpc;

public interface IXmlRpcHandler
{
    void SetCapabilities(XElement element);
    Task ProcessAsync(XmlRpcContext context);
}
