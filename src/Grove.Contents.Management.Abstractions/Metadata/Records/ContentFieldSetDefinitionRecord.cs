using System.Text.Json.Nodes;

namespace Grove.Contents.Management.Metadata.Records;

public class ContentFieldSetDefinitionRecord
{
    public ContentFieldSetDefinitionRecord()
    {
        ContentFieldDefinitionRecords = [];
        Settings = [];
    }

    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the settings of a part, like description, or any property that a module would attach
    /// to a part.
    /// </summary>
    public JsonObject Settings { get; set; }

    public IList<ContentFieldDefinitionRecord> ContentFieldDefinitionRecords { get; set; }
}
