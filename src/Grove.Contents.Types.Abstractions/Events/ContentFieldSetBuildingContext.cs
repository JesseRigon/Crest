using Grove.Contents.Management.Metadata.Records;

namespace Grove.Contents.Types.Events;

public sealed class ContentFieldSetBuildingContext
{
    public readonly string FieldSetName;

    public ContentFieldSetDefinitionRecord Record { get; set; }

    public ContentFieldSetBuildingContext(string partName, ContentFieldSetDefinitionRecord record)
    {
        ArgumentException.ThrowIfNullOrEmpty(partName);

        FieldSetName = partName;
        Record = record;
    }
}

