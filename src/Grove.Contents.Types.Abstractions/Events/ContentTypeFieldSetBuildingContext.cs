using Grove.Contents.Management.Metadata.Records;

namespace Grove.Contents.Types.Events;

public sealed class ContentTypeFieldSetBuildingContext
{
    public readonly string FieldSetName;

    public ContentTypeFieldSetDefinitionRecord Record { get; set; }

    public ContentTypeFieldSetBuildingContext(string partName, ContentTypeFieldSetDefinitionRecord record)
    {
        ArgumentException.ThrowIfNullOrEmpty(partName);

        FieldSetName = partName;
        Record = record;
    }
}
