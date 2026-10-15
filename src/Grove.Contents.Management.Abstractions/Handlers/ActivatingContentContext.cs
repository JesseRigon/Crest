using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Management.Handlers;

public class ActivatingContentContext : ContentContextBase
{
    public ActivatingContentContext(ContentItem contentItem) : base(contentItem)
    {
    }

    public string ContentType { get; set; }
    public ContentTypeDefinition Definition { get; set; }
}
