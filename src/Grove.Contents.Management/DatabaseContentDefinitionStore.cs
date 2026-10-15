using Grove.Contents.Management.Metadata.Records;
using Grove.Documents;

namespace Grove.Contents.Management;

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
