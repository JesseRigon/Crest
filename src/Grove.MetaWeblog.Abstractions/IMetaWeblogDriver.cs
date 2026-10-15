using Grove.ContentManagement;
using Grove.XmlRpc;
using Grove.XmlRpc.Models;

namespace Grove.MetaWeblog;

public interface IMetaWeblogDriver
{
    void SetCapabilities(Action<string, string> setCapability);
    void BuildPost(XRpcStruct rpcStruct, XmlRpcContext context, ContentItem contentItem);
    void EditPost(XRpcStruct rpcStruct, ContentItem contentItem);
}
