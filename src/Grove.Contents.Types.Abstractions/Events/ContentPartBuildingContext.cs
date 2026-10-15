using Grove.Contents.Management.Metadata.Records;

namespace Grove.Contents.Types.Events;

public sealed class ContentPartBuildingContext
{
    public readonly string PartName;

    public ContentPartDefinitionRecord Record { get; set; }

    public ContentPartBuildingContext(string partName, ContentPartDefinitionRecord record)
    {
        ArgumentException.ThrowIfNullOrEmpty(partName);

        PartName = partName;
        Record = record;
    }
}

