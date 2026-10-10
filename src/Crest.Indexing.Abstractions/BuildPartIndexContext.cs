using Crest.ContentManagement;
using Crest.ContentManagement.Metadata.Models;

namespace Crest.Indexing;

public class BuildPartIndexContext : BuildDocumentIndexContext
{
    public BuildPartIndexContext(
        ContentItemDocumentIndex documentIndex,
        ContentItem contentItem,
        IList<string> keys,
        ContentTypePartDefinition typePartDefinition,
        IContentIndexSettings settings)
        : base(documentIndex, contentItem, keys, settings)
    {
        ContentTypePartDefinition = typePartDefinition;
    }

    public ContentTypePartDefinition ContentTypePartDefinition { get; }
}
