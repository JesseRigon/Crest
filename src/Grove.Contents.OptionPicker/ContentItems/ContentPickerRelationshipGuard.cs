using System.Text.Json.Nodes;
using Grove.Fields;
using Grove.Models;
using Grove.Infrastructure.Settings;
using Grove.Contents.Fields.Relationships;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Management.Records;
using Grove.Contents.Management.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using YesSql;
using YesSql.Services;

namespace Grove.Contents.OptionPicker.ContentItems;

/// <summary>
/// The server side of option picker fields over content items, on every save path: the allowed
/// target types the field's source names (the type restriction is not the picker UI's alone)
/// and the relationship handlers the field's owner registered
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

        foreach (var part in typeDefinition.FieldSets)
        {
            foreach (var field in part.FieldSetDefinition.Fields.Where(field => field.FieldTypeDefinition.Name == nameof(OptionListField)
                && OptionSourceKeys.Split(field.GetSettings<OptionListFieldSettings>().SourceKey).Provider == OptionSourceKeys.ContentItemProvider))
            {
                var selected = item.Get<ContentFieldSet>(part.Name)?.Get<OptionListField>(field.Name)?.SelectedIds ?? [];
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

        var contained = await contentManager.PopulateAspectAsync<EmbeddedContentItemsAspect>(item);
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

    // A source restricted to types (or stereotypes) refuses a target of any other type; the
    // all-types source refuses a type with a stereotype. An id that resolves to no item is left
    // to the handlers - a stale reference must not block an unrelated save.
    private async Task CheckTargetTypesAsync(ContentFieldDefinition field, string[] selected, List<string> errors)
    {
        var scope = ContentItemSourceScope.Parse(OptionSourceKeys.Split(field.GetSettings<OptionListFieldSettings>().SourceKey).Qualifier);

        var rows = await session.QueryIndex<ContentItemIndex>(index => index.ContentItemId.IsIn(selected) && index.Latest).ListAsync();
        foreach (var contentType in rows.Select(row => row.ContentType).Distinct(StringComparer.Ordinal))
        {
            if (scope.Types.Contains(contentType, StringComparer.Ordinal))
            {
                continue;
            }

            var stereotype = (await definitions.GetTypeDefinitionAsync(contentType))?.GetStereotype();
            if (scope.AllTypes ? string.IsNullOrEmpty(stereotype) : !string.IsNullOrEmpty(stereotype) && scope.Stereotypes.Contains(stereotype, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            errors.Add($"The {field.DisplayName()} field cannot point at a {contentType}.");
        }
    }
}
