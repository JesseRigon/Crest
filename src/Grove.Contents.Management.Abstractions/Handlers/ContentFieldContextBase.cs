using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Management.Handlers;

public class ContentFieldContextBase
{
    protected ContentFieldContextBase(ContentItem contentItem)
    {
        ContentItem = contentItem;
    }

    public ContentItem ContentItem { get; private set; }

    public ContentPartFieldDefinition ContentPartFieldDefinition { get; set; }

    public string PartName { get; set; }
}
