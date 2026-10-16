using Grove.Contents.Management.Metadata.Records;

namespace Grove.Contents.Types.Events;

public sealed class ContentFieldBuildingContext
{
    public readonly string FieldName;

    public ContentFieldDefinitionRecord Record { get; set; }

    public ContentFieldBuildingContext(string fieldName, ContentFieldDefinitionRecord record)
    {
        ArgumentException.ThrowIfNullOrEmpty(fieldName);

        FieldName = fieldName;
        Record = record;
    }
}

