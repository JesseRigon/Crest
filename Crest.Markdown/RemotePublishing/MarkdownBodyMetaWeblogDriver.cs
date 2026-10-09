using Crest.ContentManagement;
using Crest.Markdown.Models;
using Crest.MetaWeblog;
using Crest.XmlRpc;
using Crest.XmlRpc.Models;

namespace Crest.Markdown.RemotePublishing;

public sealed class MarkdownBodyMetaWeblogDriver : MetaWeblogDriver
{
    public override void BuildPost(XRpcStruct rpcStruct, XmlRpcContext context, ContentItem contentItem)
    {
        if (!contentItem.TryGet<MarkdownBodyPart>(out var bodyPart))
        {
            return;
        }

        rpcStruct.Set("description", bodyPart.Markdown);
    }

    public override void EditPost(XRpcStruct rpcStruct, ContentItem contentItem)
    {
        if (contentItem.TryGet<MarkdownBodyPart>(out _))
        {
            contentItem.Alter<MarkdownBodyPart>(x => x.Markdown = rpcStruct.Optional<string>("description"));
        }
    }
}
