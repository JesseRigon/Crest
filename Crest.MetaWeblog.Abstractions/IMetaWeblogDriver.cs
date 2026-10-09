using Crest.ContentManagement;
using Crest.XmlRpc;
using Crest.XmlRpc.Models;

namespace Crest.MetaWeblog;

public interface IMetaWeblogDriver
{
    void SetCapabilities(Action<string, string> setCapability);
    void BuildPost(XRpcStruct rpcStruct, XmlRpcContext context, ContentItem contentItem);
    void EditPost(XRpcStruct rpcStruct, ContentItem contentItem);
}
