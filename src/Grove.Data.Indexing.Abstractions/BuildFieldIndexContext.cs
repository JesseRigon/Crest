using Grove.Contents.Management;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Data.Indexing;

public class BuildFieldIndexContext : BuildFieldSetIndexContext
{
    public BuildFieldIndexContext(
        ContentItemDocumentIndex documentIndex,
        ContentItem contentItem,
        IList<string> keys,
        ContentFieldSet contentFieldSet,
        ContentTypeFieldSetDefinition typeFieldSetDefinition,
        ContentFieldDefinition partFieldDefinition,
        IContentIndexSettings settings)
        : base(documentIndex, contentItem, keys, typeFieldSetDefinition, settings)
    {
        ContentFieldDefinition = partFieldDefinition;
        ContentFieldSet = contentFieldSet;
    }

    public ContentFieldSet ContentFieldSet { get; }
    public ContentFieldDefinition ContentFieldDefinition { get; }
}
