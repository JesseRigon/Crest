using System.Text.Encodings.Web;
using Crest.ContentManagement;
using Crest.MetaWeblog;
using Crest.Title.Models;
using Crest.XmlRpc;
using Crest.XmlRpc.Models;

namespace Crest.Title.RemotePublishing;

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
