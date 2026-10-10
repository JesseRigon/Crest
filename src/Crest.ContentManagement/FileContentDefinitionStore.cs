using Crest.ContentManagement.Metadata.Records;
using Crest.Data.Documents;
using Crest.Documents;

namespace Crest.ContentManagement;

public class FileContentDefinitionStore : IContentDefinitionStore
{
    private readonly IDocumentManager<IFileDocumentStore, ContentDefinitionRecord> _documentManager;

    public FileContentDefinitionStore(IDocumentManager<IFileDocumentStore, ContentDefinitionRecord> documentManager)
    {
        _documentManager = documentManager;
    }

    /// <inheritdoc />
    public Task<ContentDefinitionRecord> LoadContentDefinitionAsync() => _documentManager.GetOrCreateMutableAsync();

    /// <inheritdoc />
    public Task<ContentDefinitionRecord> GetContentDefinitionAsync() => _documentManager.GetOrCreateImmutableAsync();

    /// <inheritdoc />
    public Task SaveContentDefinitionAsync(ContentDefinitionRecord contentDefinitionRecord) => _documentManager.UpdateAsync(contentDefinitionRecord);
}
