using Grove.ContentManagement;
using Grove.Html.Models;
using Grove.MetaWeblog;
using Grove.XmlRpc;
using Grove.XmlRpc.Models;

namespace Grove.Html.RemotePublishing;

public sealed class HtmlBodyMetaWeblogDriver : MetaWeblogDriver
{
    public override void BuildPost(XRpcStruct rpcStruct, XmlRpcContext context, ContentItem contentItem)
    {
        if (!contentItem.TryGet<HtmlBodyPart>(out var bodyPart))
        {
            return;
        }

        rpcStruct.Set("description", bodyPart.Html);
    }

    public override void EditPost(XRpcStruct rpcStruct, ContentItem contentItem)
    {
        if (contentItem.TryGet<HtmlBodyPart>(out _))
        {
            contentItem.Alter<HtmlBodyPart>(x => x.Html = rpcStruct.Optional<string>("description"));
        }
    }
}
