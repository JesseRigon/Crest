using Microsoft.Extensions.Logging;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Data.Indexing;
using Grove.Modules;

namespace Grove.Contents.Indexing;

/// <summary>
/// Enumerates all parts and fields of content item to extract indexed properties.
/// </summary>
public class ContentItemIndexCoordinator : IDocumentIndexHandler
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ITypeActivatorFactory<ContentFieldSet> _contentFieldSetFactory;
    private readonly IEnumerable<IContentFieldSetIndexHandler> _partIndexHandlers;
    private readonly IEnumerable<IContentFieldIndexHandler> _fieldIndexHandlers;
    private readonly ILogger _logger;

    public ContentItemIndexCoordinator(
        IContentDefinitionManager contentDefinitionManager,
        ITypeActivatorFactory<ContentFieldSet> contentFieldSetFactory,
        IEnumerable<IContentFieldSetIndexHandler> partIndexHandlers,
        IEnumerable<IContentFieldIndexHandler> fieldIndexHandlers,
        ILogger<ContentItemIndexCoordinator> logger)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _contentFieldSetFactory = contentFieldSetFactory;
        _partIndexHandlers = partIndexHandlers;
        _fieldIndexHandlers = fieldIndexHandlers;
        _logger = logger;
    }

    public async Task BuildIndexAsync(BuildDocumentIndexContext context)
    {
        if (context.Record is not ContentItem contentItem)
        {
            return;
        }

        var contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);

        if (contentTypeDefinition == null)
        {
            return;
        }

        foreach (var contentTypeFieldSetDefinition in contentTypeDefinition.FieldSets)
        {
            var partName = contentTypeFieldSetDefinition.Name;
            var fieldSetTypeName = contentTypeFieldSetDefinition.FieldSetDefinition.Name;
            var partActivator = _contentFieldSetFactory.GetTypeActivator(fieldSetTypeName);
            var part = (ContentFieldSet)contentItem.Get(partActivator.Type, partName);

            var contentTypeFieldSetDefinitionMethod = contentTypeFieldSetDefinition.GetType().GetMethod("GetSettings");
            var contentTypeFieldSetDefinitionGeneric = contentTypeFieldSetDefinitionMethod.MakeGenericMethod(context.Settings.GetType());
            var typeFieldSetIndexSettings = (IContentIndexSettings)contentTypeFieldSetDefinitionGeneric.Invoke(contentTypeFieldSetDefinition, null);

            // Skip this part if it's not included in the index and it's not the default type part
            if (contentTypeDefinition.Name != fieldSetTypeName && !typeFieldSetIndexSettings.Included)
            {
                continue;
            }

            await _partIndexHandlers.InvokeAsync((handler, part, contentTypeFieldSetDefinition, context, typeFieldSetIndexSettings) =>
                handler.BuildIndexAsync(part, contentTypeFieldSetDefinition, context, typeFieldSetIndexSettings),
                    part, contentTypeFieldSetDefinition, context, typeFieldSetIndexSettings, _logger);

            foreach (var contentFieldDefinition in contentTypeFieldSetDefinition.FieldSetDefinition.Fields)
            {
                var contentFieldDefinitionMethod = contentFieldDefinition.GetType().GetMethod("GetSettings");
                var contentFieldDefinitionGeneric = contentFieldDefinitionMethod.MakeGenericMethod(context.Settings.GetType());
                var partFieldIndexSettings = (IContentIndexSettings)contentFieldDefinitionGeneric.Invoke(contentFieldDefinition, null);

                if (!partFieldIndexSettings.Included)
                {
                    continue;
                }

                await _fieldIndexHandlers.InvokeAsync((handler, part, contentTypeFieldSetDefinition, contentFieldDefinition, context, partFieldIndexSettings) =>
                    handler.BuildIndexAsync(part, contentTypeFieldSetDefinition, contentFieldDefinition, context, partFieldIndexSettings),
                        part, contentTypeFieldSetDefinition, contentFieldDefinition, context, partFieldIndexSettings, _logger);
            }
        }

        return;
    }
}
