using Crest.Data.Documents;

namespace Crest.ContentManagement.Metadata.Records;

[FileDocumentStore(FileName = "ContentDefinition")]
public class ContentDefinitionRecord : Document
{
    public ContentDefinitionRecord()
    {
        ContentTypeDefinitionRecords = [];
        ContentPartDefinitionRecords = [];
    }

    public IList<ContentTypeDefinitionRecord> ContentTypeDefinitionRecords { get; set; }
    public IList<ContentPartDefinitionRecord> ContentPartDefinitionRecords { get; set; }
}
