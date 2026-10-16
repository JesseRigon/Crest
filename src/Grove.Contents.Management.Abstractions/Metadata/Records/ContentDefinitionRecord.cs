using Grove.Data.Documents;

namespace Grove.Contents.Management.Metadata.Records;

[FileDocumentStore(FileName = "ContentDefinition")]
public class ContentDefinitionRecord : Document
{
    public ContentDefinitionRecord()
    {
        ContentTypeDefinitionRecords = [];
        ContentFieldSetDefinitionRecords = [];
    }

    public IList<ContentTypeDefinitionRecord> ContentTypeDefinitionRecords { get; set; }
    public IList<ContentFieldSetDefinitionRecord> ContentFieldSetDefinitionRecords { get; set; }
}
