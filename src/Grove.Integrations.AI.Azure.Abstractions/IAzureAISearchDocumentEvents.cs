using Azure.Search.Documents.Models;

namespace Grove.Integrations.AI.Azure;

public interface IAzureAISearchDocumentEvents
{
    Task UploadingAsync(IEnumerable<SearchDocument> docs);

    Task MergingOrUploadingAsync(IEnumerable<SearchDocument> docs);
}
