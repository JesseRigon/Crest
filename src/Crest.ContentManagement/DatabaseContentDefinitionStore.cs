using Crest.ContentManagement.Metadata.Records;
using Crest.Documents;

namespace Crest.ContentManagement;

public class DatabaseContentDefinitionStore : IContentDefinitionStore
{
    private readonly IDocumentManager<ContentDefinitionRecord> _documentManager;

    public DatabaseContentDefinitionStore(IDocumentManager<ContentDefinitionRecord> documentManager)
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
