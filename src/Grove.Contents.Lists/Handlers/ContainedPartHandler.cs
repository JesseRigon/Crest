using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Lists.Models;
using Grove.Contents.Lists.Services;

namespace Grove.Contents.Lists.Handlers;

public class ContainedPartHandler : ContentHandlerBase
{
    private readonly IServiceProvider _serviceProvider;

    internal readonly IStringLocalizer S;

    private Dictionary<string, ContentTypeDefinition> _containedContentTypes;

    public ContainedPartHandler(
        IServiceProvider serviceProvider,
        IStringLocalizer<ContainedPartHandler> localizer)
    {
        _serviceProvider = serviceProvider;
        S = localizer;
    }

    public override async Task ValidatingAsync(ValidateContentContext context)
    {
        var contentType = context.ContentItem.ContentType;

        var containedContentTypes = await GetContainedContentTypesAsync();

        if (!containedContentTypes.TryGetValue(contentType, out var definition))
        {
            return;
        }

        if (definition.IsCreatable())
        {
            // If the content type is creatable, it means it can be created outside of a list, so we don't require the ListContentItemId to be set.
            return;
        }

        if (!context.ContentItem.TryGet<ContainedPart>(out var containedPart))
        {
            return;
        }

        if (string.IsNullOrEmpty(containedPart.ListContentItemId))
        {
            if (string.IsNullOrEmpty(containedPart.ListContentType))
            {
                return;
            }

            context.Fail(S["The content item of type '{0}' must have a valid ListContentItemId as it is contained by a list.", contentType], nameof(ContainedPart.ListContentItemId));
        }
    }

    private async Task<Dictionary<string, ContentTypeDefinition>> GetContainedContentTypesAsync()
    {
        if (_containedContentTypes == null)
        {
            // Resolve from DI to avoid circular references.
            var contentDefinitionManager = _serviceProvider.GetRequiredService<IContentDefinitionManager>();
            var typeDefinitions = await contentDefinitionManager.ListTypeDefinitionsAsync();

            // Build a lookup of all type definitions by name for quick access.
            var typeDefinitionMap = typeDefinitions.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

            _containedContentTypes = new Dictionary<string, ContentTypeDefinition>(StringComparer.OrdinalIgnoreCase);

            foreach (var typeDefinition in typeDefinitions)
            {
                var listPartDefinition = typeDefinition.Parts.FirstOrDefault(p =>
                    string.Equals(p.PartDefinition.Name, nameof(ListPart), StringComparison.Ordinal));

                if (listPartDefinition == null)
                {
                    continue;
                }

                var settings = listPartDefinition.GetSettings<ListPartSettings>();

                if (settings.ContainedContentTypes != null)
                {
                    foreach (var containedContentType in settings.ContainedContentTypes)
                    {
                        if (string.IsNullOrEmpty(containedContentType))
                        {
                            continue;
                        }

                        if (typeDefinitionMap.TryGetValue(containedContentType, out var containedTypeDef))
                        {
                            _containedContentTypes.TryAdd(containedContentType, containedTypeDef);
                        }
                    }
                }
            }
        }

        return _containedContentTypes;
    }

    /// <summary>
    /// A new item created into a list (the native create page with the list as its container
    /// writes <c>ContainedPart.ListContentItemId</c>): the list's type is taken from the list,
    /// and an ordered list puts the item last.
    /// </summary>
    public override async Task CreatingAsync(CreateContentContext context)
    {
        if (!context.ContentItem.TryGet<ContainedPart>(out var containedPart) || string.IsNullOrEmpty(containedPart.ListContentItemId))
        {
            return;
        }

        // Resolve from DI to avoid circular references.
        var contentManager = _serviceProvider.GetRequiredService<IContentManager>();
        var list = await contentManager.GetAsync(containedPart.ListContentItemId, VersionOptions.Latest);
        if (list is null)
        {
            return;
        }

        var contentDefinitionManager = _serviceProvider.GetRequiredService<IContentDefinitionManager>();
        var settings = (await contentDefinitionManager.GetTypeDefinitionAsync(list.ContentType))?.Parts
            .FirstOrDefault(part => string.Equals(part.PartDefinition.Name, nameof(ListPart), StringComparison.Ordinal))
            ?.GetSettings<ListPartSettings>();
        var order = containedPart.Order;
        if (settings?.EnableOrdering == true && order == 0)
        {
            order = await _serviceProvider.GetRequiredService<IContainerService>().GetNextOrderNumberAsync(list.ContentItemId);
        }

        context.ContentItem.Alter<ContainedPart>(part =>
        {
            part.ListContentType = list.ContentType;
            part.Order = order;
        });
    }

    public override async Task CloningAsync(CloneContentContext context)
    {
        if (context.CloneContentItem.TryGet<ContainedPart>(out var containedPart))
        {
            // Resolve from DI to avoid circular references.
            var contentManager = _serviceProvider.GetRequiredService<IContentManager>();

            var listContentItem = await contentManager.GetAsync(containedPart.ListContentItemId);
            if (listContentItem != null)
            {
                var contentDefinitionManager = _serviceProvider.GetRequiredService<IContentDefinitionManager>();
                var contentTypeDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(listContentItem.ContentType);
                var contentTypePartDefinition = contentTypeDefinition.Parts.FirstOrDefault(x => string.Equals(x.PartDefinition.Name, "ListPart", StringComparison.Ordinal));
                var settings = contentTypePartDefinition.GetSettings<ListPartSettings>();
                if (settings.EnableOrdering)
                {
                    var containerService = _serviceProvider.GetRequiredService<IContainerService>();
                    var nextOrder = await containerService.GetNextOrderNumberAsync(containedPart.ListContentItemId);
                    context.CloneContentItem.Alter<ContainedPart>(x => x.Order = nextOrder);
                }
            }
        }
    }
}
