using Grove.AzureAI.Models;

namespace Grove.AzureAI;

public interface IAzureAISearchFieldIndexEvents
{
    Task MappingAsync(SearchIndexDefinition context);

    Task MappedAsync(SearchIndexDefinition context);
}
