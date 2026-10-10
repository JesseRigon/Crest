using Crest.ContentManagement.Metadata.Records;

namespace Crest.ContentTypes.Events;

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

