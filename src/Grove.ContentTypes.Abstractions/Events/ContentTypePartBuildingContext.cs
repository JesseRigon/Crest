using Grove.ContentManagement.Metadata.Records;

namespace Grove.ContentTypes.Events;

public sealed class ContentTypePartBuildingContext
{
    public readonly string PartName;

    public ContentTypePartDefinitionRecord Record { get; set; }

    public ContentTypePartBuildingContext(string partName, ContentTypePartDefinitionRecord record)
    {
        ArgumentException.ThrowIfNullOrEmpty(partName);

        PartName = partName;
        Record = record;
    }
}
