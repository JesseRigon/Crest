using System.Xml.Linq;

namespace Grove.XmlRpc;

public interface IXmlRpcHandler
{
    void SetCapabilities(XElement element);
    Task ProcessAsync(XmlRpcContext context);
}
