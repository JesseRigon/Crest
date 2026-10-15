using Grove.Integrations.AI.Azure.Models;

namespace Grove.Integrations.AI.Azure.Handlers;

public class AzureAISearchFieldIndexEventsBase : IAzureAISearchFieldIndexEvents
{
    public virtual Task MappingAsync(SearchIndexDefinition context)
        => Task.CompletedTask;

    public virtual Task MappedAsync(SearchIndexDefinition context)
        => Task.CompletedTask;
}
