using System.Text.Json.Nodes;
using Grove.ContentFields.Fields;
using Grove.ContentFields.Relationships;
using Grove.ContentFields.Settings;
using Grove.ContentManagement;
using Grove.ContentManagement.Handlers;
using Grove.ContentManagement.Metadata;
using Grove.ContentManagement.Metadata.Models;
using Grove.ContentManagement.Records;
using Grove.ContentManagement.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using YesSql;
using YesSql.Services;

namespace Grove.ContentFields.Services;

/// <summary>
/// The server side of content picker fields, on every save path: the allowed target
/// types the field's settings declare (until this ran, the type restriction was the
/// picker UI's alone) and the relationship handlers the field's owner registered
/// (<see cref="IContentPickerRelationshipHandler"/>). Walks the saved item and the items
/// contained in it (bag elements), since pickers live in both.
/// </summary>
/// <remarks>Never depends on <see cref="IContentManager"/> (it is constructed by it);
/// handlers are resolved when they run, so they may.</remarks>
public sealed class ContentPickerRelationshipGuard(
    IContentDefinitionManager definitions,
    ISession session,
    IServiceProvider services,
    IOptions<ContentPickerRelationshipOptions> options) : IContentSaveGuard
{
    private const int MaxDepth = 4;

    public async Task GuardAsync(ContentSaveGuardContext context)
    {
        var errors = new List<string>();
        await CheckItemAsync(context.ContentItem, context.ContentItem, context.ContentManager, errors, 0);
        foreach (var error in errors)
        {
            context.Fail(error);
        }
    }

    private async Task CheckItemAsync(ContentItem root, ContentItem item, IContentManager contentManager, List<string> errors, int depth)
    {
        var typeDefinition = await definitions.GetTypeDefinitionAsync(item.ContentType);
        if (typeDefinition is null)
        {
            return;
        }

        foreach (var part in typeDefinition.Parts)
        {
            foreach (var field in part.PartDefinition.Fields.Where(field => field.FieldDefinition.Name == nameof(ContentPickerField)))
            {
                var selected = item.Get<ContentPart>(part.Name)?.Get<ContentPickerField>(field.Name)?.ContentItemIds ?? [];
                if (selected.Length == 0)
                {
                    continue;
                }

                await CheckTargetTypesAsync(field, selected, errors);

                foreach (var handlerType in options.Value.For(part.Name, field.Name))
                {
                    var handler = (IContentPickerRelationshipHandler)services.GetRequiredService(handlerType);
                    await handler.ValidateAsync(new ContentPickerRelationshipContext(root, item, part.Name, field.Name, selected, errors));
                }
            }
        }

        if (depth >= MaxDepth)
        {
            return;
        }

        var contained = await contentManager.PopulateAspectAsync<ContainedContentItemsAspect>(item);
        foreach (var accessor in contained.Accessors)
        {
            foreach (var node in accessor.Invoke((JsonObject)item.Content).OfType<JsonObject>())
            {
                if (node.ToObject<ContentItem>() is { } child)
                {
                    await CheckItemAsync(root, child, contentManager, errors, depth + 1);
                }
            }
        }
    }

    // A picker restricted to types (or stereotypes) refuses a target of any other type.
    // An unrestricted picker (all types, or no restriction declared) accepts any; an id
    // that resolves to no item is left to the handlers - a stale reference must not block
    // an unrelated save.
    private async Task CheckTargetTypesAsync(ContentPartFieldDefinition field, string[] selected, List<string> errors)
    {
        var settings = field.GetSettings<ContentPickerFieldSettings>();
        if (settings.DisplayAllContentTypes || (settings.DisplayedContentTypes.Length == 0 && settings.DisplayedStereotypes.Length == 0))
        {
            return;
        }

        var rows = await session.QueryIndex<ContentItemIndex>(index => index.ContentItemId.IsIn(selected) && index.Latest).ListAsync();
        foreach (var contentType in rows.Select(row => row.ContentType).Distinct(StringComparer.Ordinal))
        {
            if (settings.DisplayedContentTypes.Contains(contentType, StringComparer.Ordinal))
            {
                continue;
            }

            var stereotype = (await definitions.GetTypeDefinitionAsync(contentType))?.GetStereotype();
            if (!string.IsNullOrEmpty(stereotype) && settings.DisplayedStereotypes.Contains(stereotype, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            errors.Add($"The {field.DisplayName()} field cannot point at a {contentType}.");
        }
    }
}
