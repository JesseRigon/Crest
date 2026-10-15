using Grove.Integrations.AI.Azure.Models;

namespace Grove.Integrations.AI.Azure;

public interface IAzureAISearchFieldIndexEvents
{
    Task MappingAsync(SearchIndexDefinition context);

    Task MappedAsync(SearchIndexDefinition context);
}
