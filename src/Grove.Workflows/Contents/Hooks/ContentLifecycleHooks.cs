using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata;
using Grove.Workflows.Contents.Activities;
using Grove.Workflows.Contents.Stimuli;
using Grove.Workflows.Contexts;
using Grove.Workflows.Hooks;
using Grove.Workflows.Runtime;
using Grove.Workflows.Runtime.Filters;
using Grove.Workflows.Units;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Workflows.Contents.Hooks;

/// <summary>The workflow input keys of a content lifecycle hook or trigger, beside the actor.</summary>
public static class ContentLifecycleInputKeys
{
    /// <summary>The item's new state (<see cref="ContentItem"/>).</summary>
    public const string ContentItem = nameof(Grove.Contents.Management.ContentItem);

    /// <summary>The item's state before the change; absent when it is being created.</summary>
    public const string PreviousContentItem = nameof(PreviousContentItem);

    /// <summary>The lifecycle point (<see cref="ContentLifecyclePoints"/>).</summary>
    public const string Point = "ContentLifecyclePoint";
}

/// <summary>
/// The content lifecycle's hook slots (docs/workflows.md › Hook slots): one per content type and
/// lifecycle point, keyed <c>content:{Type}.{point}</c> (<c>content:Invoice.publishing</c>).
/// </summary>
public static class ContentHookSlots
{
    public const string Prefix = "content:";

    public const string Object = "content";

    public static string Key(string contentType, string point) => $"{Prefix}{contentType}.{point}";

    public static bool TryParse(string? key, out string contentType, out string point)
    {
        contentType = point = string.Empty;
        if (key is null || !key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rest = key[Prefix.Length..];
        var dot = rest.LastIndexOf('.');
        if (dot <= 0 || dot == rest.Length - 1)
        {
            return false;
        }

        contentType = rest[..dot];
        point = rest[(dot + 1)..].ToLowerInvariant();
        return ContentLifecyclePoints.IsPoint(point);
    }

    public static string Label(string point) => point switch
    {
        ContentLifecyclePoints.Creating => "creating",
        ContentLifecyclePoints.Updating => "updating",
        ContentLifecyclePoints.Validating => "validating",
        ContentLifecyclePoints.SavingDraft => "saving a draft",
        ContentLifecyclePoints.Publishing => "publishing",
        ContentLifecyclePoints.Unpublishing => "unpublishing",
        ContentLifecyclePoints.Removing => "removing",
        ContentLifecyclePoints.Cloning => "cloning",
        ContentLifecyclePoints.Importing => "importing",
        ContentLifecyclePoints.Localizing => "translating",
        _ => point,
    };

    /// <summary>The trigger activity raised after the point's unit commits.</summary>
    public static string? TriggerActivityTypeName(string point) => point switch
    {
        ContentLifecyclePoints.Creating => Helpers.ActivityTypeNameHelper.GenerateTypeName<ContentCreated>(),
        ContentLifecyclePoints.Updating => Helpers.ActivityTypeNameHelper.GenerateTypeName<ContentUpdated>(),
        ContentLifecyclePoints.Validating => Helpers.ActivityTypeNameHelper.GenerateTypeName<ContentValidated>(),
        ContentLifecyclePoints.SavingDraft => Helpers.ActivityTypeNameHelper.GenerateTypeName<ContentDraftSaved>(),
        ContentLifecyclePoints.Publishing => Helpers.ActivityTypeNameHelper.GenerateTypeName<ContentPublished>(),
        ContentLifecyclePoints.Unpublishing => Helpers.ActivityTypeNameHelper.GenerateTypeName<ContentUnpublished>(),
        ContentLifecyclePoints.Removing => Helpers.ActivityTypeNameHelper.GenerateTypeName<ContentRemoved>(),
        ContentLifecyclePoints.Cloning => Helpers.ActivityTypeNameHelper.GenerateTypeName<ContentCloned>(),
        ContentLifecyclePoints.Importing => Helpers.ActivityTypeNameHelper.GenerateTypeName<ContentImported>(),
        ContentLifecyclePoints.Localizing => Helpers.ActivityTypeNameHelper.GenerateTypeName<ContentLocalized>(),
        _ => null,
    };

    // Workflow definitions are content items too; their lifecycle runs no flows.
    internal static bool IsExcluded(string contentType) => string.Equals(contentType, "WorkflowDefinition", StringComparison.Ordinal);
}

/// <summary>
/// The content types' hook slots, read from the tenant's definitions when asked, and their system
/// attachments: a module's <see cref="WorkflowFieldSetHookAttachmentDescriptor"/> resolves to the
/// point's slot of every type that attaches the field set.
/// </summary>
public sealed class ContentHookSlotSource(
    IContentDefinitionManager definitions,
    IEnumerable<IWorkflowFieldSetHookAttachmentProvider> fieldSetAttachmentProviders,
    IEnumerable<IWorkflowWeldedFieldSetSource> weldedFieldSets) : IWorkflowHookSlotSource
{
    private IReadOnlyList<WorkflowFieldSetHookAttachmentDescriptor>? _fieldSetAttachments;

    private IReadOnlyList<WorkflowFieldSetHookAttachmentDescriptor> FieldSetAttachments =>
        _fieldSetAttachments ??= fieldSetAttachmentProviders.SelectMany(provider => provider.FieldSetHookAttachments).ToArray();

    public async Task<IReadOnlyList<WorkflowHookSlotDescriptor>> ListHookSlotsAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<WorkflowHookSlotDescriptor>();
        var position = 0;
        foreach (var type in (await definitions.ListTypeDefinitionsAsync()).Where(type => !ContentHookSlots.IsExcluded(type.Name)).OrderBy(type => type.DisplayName ?? type.Name, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var point in ContentLifecyclePoints.All)
            {
                result.Add(Describe(type.Name, type.DisplayName, point, 2000 + position++));
            }
        }

        return result;
    }

    public async Task<WorkflowHookSlotDescriptor?> FindHookSlotAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!ContentHookSlots.TryParse(key, out var contentType, out var point) || ContentHookSlots.IsExcluded(contentType))
        {
            return null;
        }

        var type = await definitions.GetTypeDefinitionAsync(contentType);
        return type is null ? null : Describe(type.Name, type.DisplayName, point, 2000);
    }

    public async Task<IReadOnlyList<WorkflowHookAttachmentDescriptor>> ListHookAttachmentsAsync(WorkflowHookSlotDescriptor slot, CancellationToken cancellationToken = default)
    {
        if (FieldSetAttachments.Count == 0 || !ContentHookSlots.TryParse(slot.Key, out var contentType, out var point))
        {
            return [];
        }

        var type = await definitions.GetTypeDefinitionAsync(contentType);
        if (type is null)
        {
            return [];
        }

        var fieldSets = type.FieldSets.Select(fieldSet => fieldSet.FieldSetDefinition.Name).ToHashSet(StringComparer.Ordinal);

        // A field set a module welds on the type's items brings its system flows as an attached one does.
        foreach (var source in weldedFieldSets)
        {
            fieldSets.UnionWith(await source.FieldSetsAsync(contentType, cancellationToken));
        }
        return FieldSetAttachments
            .Where(attachment => string.Equals(attachment.Point, point, StringComparison.Ordinal) && fieldSets.Contains(attachment.FieldSet))
            .Select(attachment => new WorkflowHookAttachmentDescriptor(slot.Key, attachment.FlowKey, attachment.Position, attachment.Required))
            .ToArray();
    }

    // The types, their field sets and so their attachments change with the content definitions.
    public Task<string> StampAsync(CancellationToken cancellationToken = default) => definitions.GetIdentifierAsync();

    private static WorkflowHookSlotDescriptor Describe(string contentType, string? displayName, string point, int position) => new(
        ContentHookSlots.Key(contentType, point),
        $"{displayName ?? contentType}: {ContentHookSlots.Label(point)}",
        ContentHookSlots.Object,
        $"Runs inside the unit of work that is {ContentHookSlots.Label(point)} a {displayName ?? contentType}, before it is stored. A required attachment that fails refuses the change, with its errors under the fields they name.",
        position);
}

/// <summary>
/// The content lifecycle's workflow side (docs/content-engine.md › Behaviour is workflows): at
/// each point, the flows attached to the type's hook slot run inside the saving unit of work, and
/// a required one that fails refuses the change with its errors by field path; once the unit
/// commits, the point's trigger starts the flows subscribed to it. Hooks and triggers both carry
/// the item's previous state and its new one; the previous state is read only when a hook is
/// attached, or when a flow subscribes to the point's trigger for the type. The workflow services
/// are resolved when first used: the definition store they reach goes through the content manager.
/// </summary>
public sealed class WorkflowContentLifecycleHandler(IServiceProvider services) : IContentLifecycleHandler
{
    private readonly Dictionary<(string Point, string ContentType), bool> _subscribed = [];

    /// <summary>Whether a flow starts or waits on the point's trigger for this type.</summary>
    public async ValueTask<bool> NeedsPreviousAfterStoreAsync(string point, string contentType)
    {
        if (ContentHookSlots.IsExcluded(contentType) || ContentHookSlots.TriggerActivityTypeName(point) is not { } activityType)
        {
            return false;
        }

        if (_subscribed.TryGetValue((point, contentType), out var subscribed))
        {
            return subscribed;
        }

        var hash = services.GetRequiredService<IStimulusHasher>().Hash(activityType, new ContentEventStimulus(contentType));
        subscribed = (await services.GetRequiredService<ITriggerStore>().FindManyAsync(new TriggerFilter { Hash = hash })).Any()
            || (await services.GetRequiredService<IBookmarkStore>().FindManyAsync(new BookmarkFilter { Hash = hash })).Any();
        return _subscribed[(point, contentType)] = subscribed;
    }

    public async Task HookAsync(ContentLifecycleContext context)
    {
        var item = context.ContentItem;
        if (ContentHookSlots.IsExcluded(item.ContentType))
        {
            return;
        }

        var slot = ContentHookSlots.Key(item.ContentType, context.Point);
        if ((await services.GetRequiredService<WorkflowHookService>().ListAsync(slot)).Count == 0)
        {
            return;
        }

        var input = await InputAsync(context);
        input[WorkflowsConstants.InputKeys.Payload] = new Dictionary<string, object>
        {
            ["ContentItemId"] = item.ContentItemId ?? string.Empty,
            ["ContentType"] = item.ContentType,
            [ContentLifecycleInputKeys.Point] = context.Point,
        };

        try
        {
            await services.GetRequiredService<WorkflowHookRunner>().RunAsync(slot, item.ContentItemId, input, parentInstanceId: null, failingActivityId: null);
        }
        catch (WorkflowHookFailedException failed)
        {
            foreach (var error in failed.Errors)
            {
                context.Fail(error.Path, error.Message);
            }
        }
    }

    public async Task TriggerAsync(ContentLifecycleContext context)
    {
        var item = context.ContentItem;
        if (ContentHookSlots.IsExcluded(item.ContentType) || ContentHookSlots.TriggerActivityTypeName(context.Point) is not { } activityType)
        {
            return;
        }

        // Fires after the current unit commits: a flow never sees an item whose write was rolled
        // back (docs/workflows.md › Posting on workflows).
        await services.GetRequiredService<WorkflowStimulusQueue>().EnqueueAsync(activityType, new ContentEventStimulus(item.ContentType), new()
        {
            CorrelationId = item.ContentItemId,
            Input = await InputAsync(context),
        });
    }

    private async Task<Dictionary<string, object>> InputAsync(ContentLifecycleContext context)
    {
        var input = new Dictionary<string, object>
        {
            [ContentLifecycleInputKeys.ContentItem] = context.ContentItem,
            [ContentLifecycleInputKeys.Point] = context.Point,
            [WorkflowUserContext.InputKey] = services.GetRequiredService<IWorkflowUserContextAccessor>().CaptureSealed(),
        };

        if (await context.GetPreviousContentItemAsync() is { } previous)
        {
            input[ContentLifecycleInputKeys.PreviousContentItem] = previous;
        }

        return input;
    }
}
