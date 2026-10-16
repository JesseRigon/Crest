using Grove.Contents.Management;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Data.Indexing;

public class BuildFieldSetIndexContext : BuildDocumentIndexContext
{
    public BuildFieldSetIndexContext(
        ContentItemDocumentIndex documentIndex,
        ContentItem contentItem,
        IList<string> keys,
        ContentTypeFieldSetDefinition typeFieldSetDefinition,
        IContentIndexSettings settings)
        : base(documentIndex, contentItem, keys, settings)
    {
        ContentTypeFieldSetDefinition = typeFieldSetDefinition;
    }

    public ContentTypeFieldSetDefinition ContentTypeFieldSetDefinition { get; }
}
