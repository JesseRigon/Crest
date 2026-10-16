using Microsoft.Extensions.Logging;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Modules;

namespace Grove.Contents.Localization.Handlers;

internal sealed class ContentLocalizationFieldSetHandlerCoordinator : ContentLocalizationHandlerBase
{
    private readonly ITypeActivatorFactory<ContentFieldSet> _contentFieldSetFactory;
    private readonly IEnumerable<IContentLocalizationFieldSetHandler> _partHandlers;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ILogger _logger;

    public ContentLocalizationFieldSetHandlerCoordinator(

        ITypeActivatorFactory<ContentFieldSet> contentFieldSetFactory,
        IEnumerable<IContentLocalizationFieldSetHandler> partHandlers,
        IContentDefinitionManager contentDefinitionManager,
        ILogger<ContentLocalizationFieldSetHandlerCoordinator> logger
    )
    {
        _contentFieldSetFactory = contentFieldSetFactory;
        _partHandlers = partHandlers;
        _contentDefinitionManager = contentDefinitionManager;
        _logger = logger;
    }

    public override async Task LocalizingAsync(LocalizationContentContext context)
    {
        var contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(context.ContentItem.ContentType);
        if (contentTypeDefinition == null)
        {
            return;
        }

        foreach (var typeFieldSetDefinition in contentTypeDefinition.FieldSets)
        {
            var partName = typeFieldSetDefinition.FieldSetDefinition.Name;
            var activator = _contentFieldSetFactory.GetTypeActivator(partName);
            var part = context.ContentItem.Get(activator.Type, typeFieldSetDefinition.Name) as ContentFieldSet;

            if (part != null)
            {
                await _partHandlers.InvokeAsync((handler, context, part) => handler.LocalizingAsync(context, part), context, part, _logger);
            }
        }
    }

    public override async Task LocalizedAsync(LocalizationContentContext context)
    {
        var contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(context.ContentItem.ContentType);
        if (contentTypeDefinition == null)
        {
            return;
        }

        foreach (var typeFieldSetDefinition in contentTypeDefinition.FieldSets)
        {
            var partName = typeFieldSetDefinition.FieldSetDefinition.Name;
            var activator = _contentFieldSetFactory.GetTypeActivator(partName);
            var part = context.ContentItem.Get(activator.Type, typeFieldSetDefinition.Name) as ContentFieldSet;

            if (part != null)
            {
                await _partHandlers.InvokeAsync((handler, context, part) => handler.LocalizedAsync(context, part), context, part, _logger);
            }
        }
    }
}
