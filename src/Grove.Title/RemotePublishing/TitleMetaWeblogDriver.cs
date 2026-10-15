using System.Text.Encodings.Web;
using Grove.ContentManagement;
using Grove.MetaWeblog;
using Grove.Title.Models;
using Grove.XmlRpc;
using Grove.XmlRpc.Models;

namespace Grove.Title.RemotePublishing;

public sealed class TitleMetaWeblogDriver : MetaWeblogDriver
{
    private readonly HtmlEncoder _encoder;

    public TitleMetaWeblogDriver(HtmlEncoder encoder)
    {
        _encoder = encoder;
    }

    public override void BuildPost(XRpcStruct rpcStruct, XmlRpcContext context, ContentItem contentItem)
    {
        if (!contentItem.TryGet<TitlePart>(out var titlePart))
        {
            return;
        }

        rpcStruct.Set("title", _encoder.Encode(titlePart.Title));
    }

    public override void EditPost(XRpcStruct rpcStruct, ContentItem contentItem)
    {
        contentItem.DisplayText = rpcStruct.Optional<string>("title");
    }
}
