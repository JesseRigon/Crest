using System.Text.Json.Nodes;

namespace Grove.Contents.Management.Metadata.Records;

public class ContentTypeDefinitionRecord
{
    public ContentTypeDefinitionRecord()
    {
        ContentTypeFieldSetDefinitionRecords = [];
        EmbeddedContentDefinitionRecords = [];
    }

    public string Name { get; set; }

    public string DisplayName { get; set; }

    public JsonObject Settings { get; set; }

    public IList<ContentTypeFieldSetDefinitionRecord> ContentTypeFieldSetDefinitionRecords { get; set; }

    /// <summary>The type's embeddings (docs/content-engine.md › Embedded content items).</summary>
    public IList<EmbeddedContentDefinitionRecord> EmbeddedContentDefinitionRecords { get; set; }
}
