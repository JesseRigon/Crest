using Grove.Contents.Management.Metadata.Records;

namespace Grove.Contents.Types.Events;

public sealed class ContentTypeBuildingContext
{
    public readonly string TypeName;

    public ContentTypeDefinitionRecord Record { get; set; }

    public ContentTypeBuildingContext(string typeName, ContentTypeDefinitionRecord record)
    {
        ArgumentException.ThrowIfNullOrEmpty(typeName);

        TypeName = typeName;
        Record = record;
    }
}
