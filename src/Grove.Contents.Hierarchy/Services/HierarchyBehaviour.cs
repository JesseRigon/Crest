using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Grove.Contents.Localization.Models;
using Grove.Contents.Localization.Records;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Hierarchy.Indexes;
using Grove.Contents.Hierarchy.Models;
using Grove.Workflows;
using YesSql;

namespace Grove.Contents.Hierarchy.Services;

/// <summary>
/// What the lists' system flows do (<see cref="Workflows.HierarchyWorkflowProvider"/>): an item
/// created into a list takes the list's type and, in an ordered list, the last place, and joins the
/// list in its culture; an item of a type only lists hold must have its list; a clone goes last; a
/// translation joins the translated list, and a translated list gathers the children already in its
/// culture. The types whose items carry the welded <c>ParentContentItem</c> field set are the types
/// any list holds (<see cref="IWorkflowWeldedFieldSetSource"/>).
/// </summary>
public sealed class HierarchyBehaviour(
    IServiceProvider serviceProvider,
    ISession session,
    IStringLocalizer<HierarchyBehaviour> S) : IWorkflowWeldedFieldSetSource
{
    private Dictionary<string, ContentTypeDefinition> _containedContentTypes;

    public async Task<IReadOnlyCollection<string>> FieldSetsAsync(string contentType, CancellationToken cancellationToken = default) =>
        (await GetContainedContentTypesAsync()).ContainsKey(contentType) ? [ContentFieldSetName.Of<ParentContentItemFieldSet>()] : [];

    /// <summary>The refusal of an item of a type only lists hold that names no list; null when it is fine.</summary>
    public async Task<string> ListErrorAsync(ContentItem contentItem)
    {
        var containedContentTypes = await GetContainedContentTypesAsync();

        // A creatable type can be created outside of a list.
        if (!containedContentTypes.TryGetValue(contentItem.ContentType, out var definition) || definition.IsCreatable())
        {
            return null;
        }

        if (!contentItem.TryGet<ParentContentItemFieldSet>(out var parentContentItemFieldSet)
            || !string.IsNullOrEmpty(parentContentItemFieldSet.ListContentItemId)
            || string.IsNullOrEmpty(parentContentItemFieldSet.ListContentType))
        {
            return null;
        }

        return S["The content item of type '{0}' must have a valid ListContentItemId as it is contained by a list.", contentItem.ContentType];
    }

    /// <summary>
    /// A new item created into a list (the create page with the list as its container writes
    /// <c>ParentContentItem.ListContentItemId</c>): the list's type is taken from the list, and an
    /// ordered list puts the item last.
    /// </summary>
    public async Task FileInListAsync(ContentItem contentItem)
    {
        if (!contentItem.TryGet<ParentContentItemFieldSet>(out var parentContentItemFieldSet) || string.IsNullOrEmpty(parentContentItemFieldSet.ListContentItemId))
        {
            return;
        }

        // Resolve from DI to avoid circular references.
        var contentManager = serviceProvider.GetRequiredService<IContentManager>();
        var list = await contentManager.GetAsync(parentContentItemFieldSet.ListContentItemId, VersionOptions.Latest);
        if (list is null)
        {
            return;
        }

        var settings = await ListSettingsAsync(list.ContentType);
        var order = parentContentItemFieldSet.Order;
        if (settings?.EnableOrdering == true && order == 0)
        {
            order = await serviceProvider.GetRequiredService<IContainerService>().GetNextOrderNumberAsync(list.ContentItemId);
        }

        contentItem.Alter<ParentContentItemFieldSet>(part =>
        {
            part.ListContentType = list.ContentType;
            part.Order = order;
        });
    }

    /// <summary>An item created into a list of another culture joins the list in its own culture.</summary>
    public async Task JoinCultureListAsync(ContentItem contentItem)
    {
        if (contentItem.TryGet<LocalizationFieldSet>(out var localization) && !string.IsNullOrEmpty(localization.Culture))
        {
            await MoveToListInCultureAsync(contentItem, localization.Culture);
        }
    }

    /// <summary>A clone of an item in an ordered list goes last.</summary>
    public async Task OrderCloneAsync(ContentItem clone)
    {
        if (!clone.TryGet<ParentContentItemFieldSet>(out var parentContentItemFieldSet))
        {
            return;
        }

        // Resolve from DI to avoid circular references.
        var contentManager = serviceProvider.GetRequiredService<IContentManager>();
        var listContentItem = await contentManager.GetAsync(parentContentItemFieldSet.ListContentItemId);
        if (listContentItem != null && (await ListSettingsAsync(listContentItem.ContentType))?.EnableOrdering == true)
        {
            var nextOrder = await serviceProvider.GetRequiredService<IContainerService>().GetNextOrderNumberAsync(parentContentItemFieldSet.ListContentItemId);
            clone.Alter<ParentContentItemFieldSet>(x => x.Order = nextOrder);
        }
    }

    /// <summary>A translation of a listed item joins the translated list.</summary>
    public async Task JoinTranslatedListAsync(ContentItem translation)
    {
        if (translation.TryGet<LocalizationFieldSet>(out var localization) && !string.IsNullOrEmpty(localization.Culture))
        {
            await MoveToListInCultureAsync(translation, localization.Culture);
        }
    }

    /// <summary>A translated list gathers the children already in its culture but filed under the list it translates.</summary>
    public async Task GatherTranslatedChildrenAsync(ContentItem translation, ContentItem original)
    {
        if (original is null || !translation.TryGet<LocalizationFieldSet>(out var localization) || string.IsNullOrEmpty(localization.Culture))
        {
            return;
        }

        var containedList = await session.Query<ContentItem, ParentContentItemIndex>(x => x.ListContentItemId == original.ContentItemId).ListAsync();
        foreach (var item in containedList)
        {
            if (item.TryGet<LocalizationFieldSet>(out var itemLocalization) &&
                itemLocalization.Culture == localization.Culture &&
                item.Has<ParentContentItemFieldSet>())
            {
                item.Alter<ParentContentItemFieldSet>(part => part.ListContentItemId = translation.ContentItemId);
                await session.SaveAsync(item);
            }
        }
    }

    private async Task MoveToListInCultureAsync(ContentItem contentItem, string culture)
    {
        if (!contentItem.TryGet<ParentContentItemFieldSet>(out var parentContentItemFieldSet) || string.IsNullOrEmpty(parentContentItemFieldSet.ListContentItemId))
        {
            return;
        }

        var listId = parentContentItemFieldSet.ListContentItemId;
        var list = await session.QueryIndex<LocalizedContentItemIndex>(i => (i.Published || i.Latest) && i.ContentItemId == listId).FirstOrDefaultAsync();
        if (list is null)
        {
            return;
        }

        var normalizedCulture = culture.ToLowerInvariant();
        var localizedList = await session.QueryIndex<LocalizedContentItemIndex>(i => (i.Published || i.Latest) && i.LocalizationSet == list.LocalizationSet && i.Culture == normalizedCulture).FirstOrDefaultAsync();
        if (localizedList != null)
        {
            contentItem.Alter<ParentContentItemFieldSet>(part => part.ListContentItemId = localizedList.ContentItemId);
        }
    }

    private async Task<ChildContentItemsFieldSetSettings> ListSettingsAsync(string listContentType)
    {
        var contentDefinitionManager = serviceProvider.GetRequiredService<IContentDefinitionManager>();
        return (await contentDefinitionManager.GetTypeDefinitionAsync(listContentType))?.FieldSets
            .FirstOrDefault(part => string.Equals(part.FieldSetDefinition.Name, ContentFieldSetName.Of<ChildContentItemsFieldSet>(), StringComparison.Ordinal))
            ?.GetSettings<ChildContentItemsFieldSetSettings>();
    }

    private async Task<Dictionary<string, ContentTypeDefinition>> GetContainedContentTypesAsync()
    {
        if (_containedContentTypes == null)
        {
            // Resolve from DI to avoid circular references.
            var contentDefinitionManager = serviceProvider.GetRequiredService<IContentDefinitionManager>();
            var typeDefinitions = await contentDefinitionManager.ListTypeDefinitionsAsync();
            var typeDefinitionMap = typeDefinitions.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

            _containedContentTypes = new Dictionary<string, ContentTypeDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var typeDefinition in typeDefinitions)
            {
                var settings = typeDefinition.FieldSets
                    .FirstOrDefault(p => string.Equals(p.FieldSetDefinition.Name, ContentFieldSetName.Of<ChildContentItemsFieldSet>(), StringComparison.Ordinal))?
                    .GetSettings<ChildContentItemsFieldSetSettings>();

                foreach (var containedContentType in settings?.ContainedContentTypes ?? [])
                {
                    if (!string.IsNullOrEmpty(containedContentType) && typeDefinitionMap.TryGetValue(containedContentType, out var containedTypeDef))
                    {
                        _containedContentTypes.TryAdd(containedContentType, containedTypeDef);
                    }
                }
            }
        }

        return _containedContentTypes;
    }
}
