using Crest.AzureAI.Models;

namespace Crest.AzureAI;

public interface IAzureAISearchFieldIndexEvents
{
    Task MappingAsync(SearchIndexDefinition context);

    Task MappedAsync(SearchIndexDefinition context);
}
