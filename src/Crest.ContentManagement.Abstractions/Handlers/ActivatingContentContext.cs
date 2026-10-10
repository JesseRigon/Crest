using Crest.ContentManagement.Metadata.Models;

namespace Crest.ContentManagement.Handlers;

public class ActivatingContentContext : ContentContextBase
{
    public ActivatingContentContext(ContentItem contentItem) : base(contentItem)
    {
    }

    public string ContentType { get; set; }
    public ContentTypeDefinition Definition { get; set; }
}
