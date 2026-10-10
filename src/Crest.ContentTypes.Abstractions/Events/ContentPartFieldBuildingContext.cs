using Crest.ContentManagement.Metadata.Records;

namespace Crest.ContentTypes.Events;

public sealed class ContentPartFieldBuildingContext
{
    public readonly string FieldName;

    public ContentPartFieldDefinitionRecord Record { get; set; }

    public ContentPartFieldBuildingContext(string fieldName, ContentPartFieldDefinitionRecord record)
    {
        ArgumentException.ThrowIfNullOrEmpty(fieldName);

        FieldName = fieldName;
        Record = record;
    }
}

