using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Management;
using Grove.Data.Indexing;
using Grove.Integrations.AI.Azure.Handlers;
using Grove.Integrations.AI.Azure.Models;

namespace Grove.Integrations.AI.Azure.Flows;

/// <summary>An embedding maps as a complex collection of its allowed types' fields.</summary>
internal sealed class EmbeddedContentAzureAISearchFieldIndexEvents : AzureAISearchFieldIndexEventsBase
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IServiceProvider _serviceProvider;

    private AzureAISearchContentFieldMapper _fieldsMapper;

    public EmbeddedContentAzureAISearchFieldIndexEvents(
        IContentDefinitionManager contentDefinitionManager,
        IServiceProvider serviceProvider)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _serviceProvider = serviceProvider;
    }

    public override async Task MappingAsync(SearchIndexDefinition context)
    {
        // Lazily resolve the mapper to avoid circular dependencies.
        _fieldsMapper ??= _serviceProvider.GetService<AzureAISearchContentFieldMapper>();

        if (context.IndexEntry.Type == DocumentIndex.Types.Complex &&
            context.IndexEntry.Metadata?.TryGetValue(nameof(EmbeddedContentDefinition), out var definition) == true &&
            definition is EmbeddedContentDefinition embedding)
        {
            context.Map.SubFields ??= new List<AzureAISearchIndexMap>();

            var contentItemsField = context.Map.SubFields.FirstOrDefault(x => x.AzureFieldKey == nameof(EmbeddedContentItems.ContentItems));

            if (contentItemsField is null)
            {
                contentItemsField = new AzureAISearchIndexMap(nameof(EmbeddedContentItems.ContentItems), DocumentIndex.Types.Complex)
                {
                    IsCollection = true,
                    SubFields = new List<AzureAISearchIndexMap>(),
                };

                context.Map.SubFields.Add(contentItemsField);
            }

            var contentTypes = (await _contentDefinitionManager.ListTypeDefinitionsAsync())
                .Where(embedding.Allows)
                .Select(type => type.Name)
                .ToList();

            await _fieldsMapper.MapAsync(contentItemsField.SubFields, context.IndexProfile, contentTypes, rootFields: false);
        }
    }
}
