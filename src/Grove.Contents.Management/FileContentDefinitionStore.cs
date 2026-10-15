using Grove.Contents.Management.Metadata.Records;
using Grove.Data.Documents;
using Grove.Documents;

namespace Grove.Contents.Management;

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
