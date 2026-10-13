using System.Text.Json.Nodes;
using Crest.Access;
using Crest.ContentManagement;
using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Metadata.Models;
using Crest.Contents;
using Crest.Workflows.Activities.Flowchart.Attributes;
using Crest.Workflows.Attributes;
using Crest.Workflows.Extensions;
using Crest.Workflows.Models;
using Crest.Workflows.UIHints;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Contents;

/// <summary>The content operations (docs/workflows.md › Tasks › 3): the only writes to content outside the content manager's own callers.</summary>
public enum ContentOperationKind
{
    Create,
    Update,
    Delete,
    Publish,
    Unpublish,
}

/// <summary>What an operation did: the item it acted on, or why it did not.</summary>
public sealed record ContentOperationResult(ContentItem? Item, string? Failure)
{
    public bool Succeeded => Failure is null;

    public static ContentOperationResult Failed(string reason, ContentItem? item = null) => new(item, reason);
}

/// <summary>
/// Runs a content operation through the content manager, so validation, the content handlers
/// and the audit content handler fire as for an editor's save: the item is validated before
/// it is written, and the decision is asked again with the item as the resource, so the
/// per-type and owner variations apply (the activity gate already decided the operation's
/// own permission for the type). A denial throws like the gate does; anything else that
/// stops the operation comes back as a failure.
/// </summary>
public sealed class ContentOperationService(
    IContentManager contentManager,
    IContentDefinitionManager definitions,
    ContentFieldValueCopier fields,
    ICallerContextAccessor callers,
    IAccessDecision decision)
{
    /// <summary>The base content permission an operation is decided by (asked per type and per item).</summary>
    public static string PermissionOf(ContentOperationKind operation) => operation switch
    {
        ContentOperationKind.Delete => CommonPermissions.DeleteContent.Name,
        ContentOperationKind.Publish or ContentOperationKind.Unpublish => CommonPermissions.PublishContent.Name,
        _ => CommonPermissions.EditContent.Name,
    };

    /// <summary>The weakest permission that can allow the operation on some item of a type: the owner variation, asked per type at the gate.</summary>
    public static string GatePermissionOf(ContentOperationKind operation) =>
        CommonPermissions.OwnerPermissionsByName[PermissionOf(operation)].Name;

    public async Task<ContentOperationResult> CreateAsync(string contentType, string? displayText, IReadOnlyDictionary<string, JsonObject?> values, bool publish, CancellationToken cancellationToken = default)
    {
        var item = await contentManager.NewAsync(contentType);
        var caller = Caller(ContentOperationKind.Create, contentType);
        item.Owner = caller.UserId;
        item.Author = caller.UserName;
        if (!string.IsNullOrWhiteSpace(displayText))
        {
            item.DisplayText = displayText;
        }

        if (await ApplyAsync(item, values) is { } failure)
        {
            return ContentOperationResult.Failed(failure, item);
        }

        await RequireAsync(caller, ContentOperationKind.Create, item, cancellationToken);
        if (publish)
        {
            await RequireAsync(caller, ContentOperationKind.Publish, item, cancellationToken);
        }

        if (await ValidateAsync(item) is { } invalid)
        {
            return ContentOperationResult.Failed(invalid, item);
        }

        await contentManager.CreateAsync(item, publish ? VersionOptions.Published : VersionOptions.Draft);
        return new(item, null);
    }

    /// <summary>Sets the values on the item's latest version and saves it in place.</summary>
    public async Task<ContentOperationResult> UpdateAsync(string contentType, string contentItemId, string? displayText, IReadOnlyDictionary<string, JsonObject?> values, CancellationToken cancellationToken = default)
    {
        var item = await LoadAsync(contentType, contentItemId, VersionOptions.Latest);
        if (item.Failure is not null)
        {
            return item;
        }

        if (!string.IsNullOrWhiteSpace(displayText))
        {
            item.Item!.DisplayText = displayText;
        }

        if (await ApplyAsync(item.Item!, values) is { } failure)
        {
            return ContentOperationResult.Failed(failure, item.Item);
        }

        return await SaveAsync(item.Item!, cancellationToken);
    }

    /// <summary>
    /// The update operation's write for an item already changed in memory (Copy fields, Move
    /// fields): decided for the item, validated, then saved.
    /// </summary>
    public async Task<ContentOperationResult> SaveAsync(ContentItem item, CancellationToken cancellationToken = default)
    {
        await RequireAsync(Caller(ContentOperationKind.Update, item.ContentItemId), ContentOperationKind.Update, item, cancellationToken);
        if (await ValidateAsync(item) is { } invalid)
        {
            return ContentOperationResult.Failed(invalid, item);
        }

        await contentManager.UpdateAsync(item);
        return new(item, null);
    }

    public async Task<ContentOperationResult> DeleteAsync(string contentType, string contentItemId, CancellationToken cancellationToken = default) =>
        await ActAsync(contentType, contentItemId, ContentOperationKind.Delete, VersionOptions.Latest, contentManager.RemoveAsync, cancellationToken);

    public async Task<ContentOperationResult> PublishAsync(string contentType, string contentItemId, CancellationToken cancellationToken = default) =>
        await ActAsync(contentType, contentItemId, ContentOperationKind.Publish, VersionOptions.Latest, contentManager.PublishAsync, cancellationToken);

    public async Task<ContentOperationResult> UnpublishAsync(string contentType, string contentItemId, CancellationToken cancellationToken = default) =>
        await ActAsync(contentType, contentItemId, ContentOperationKind.Unpublish, VersionOptions.Published, contentManager.UnpublishAsync, cancellationToken);

    private async Task<ContentOperationResult> ActAsync(string contentType, string contentItemId, ContentOperationKind operation, VersionOptions version, Func<ContentItem, Task<bool>> act, CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(contentType, contentItemId, version);
        if (loaded.Failure is not null)
        {
            return loaded;
        }

        await RequireAsync(Caller(operation, contentItemId), operation, loaded.Item!, cancellationToken);
        return await act(loaded.Item!)
            ? loaded
            : ContentOperationResult.Failed($"{operation} of '{contentItemId}' was refused by a content handler.", loaded.Item);
    }

    private async Task<ContentOperationResult> LoadAsync(string contentType, string contentItemId, VersionOptions version)
    {
        var item = await contentManager.GetAsync(contentItemId, version);
        if (item is null)
        {
            return ContentOperationResult.Failed($"Item '{contentItemId}' was not found.");
        }

        // The descriptor fixes the type: an id that names an item of another type is refused,
        // so a node built for one type never writes another.
        return string.Equals(item.ContentType, contentType, StringComparison.Ordinal)
            ? new(item, null)
            : ContentOperationResult.Failed($"Item '{contentItemId}' is a {item.ContentType}, not a {contentType}.", item);
    }

    /// <summary>Writes each value onto its field; null leaves a field as it is. Fails on a path the type does not have.</summary>
    private async Task<string?> ApplyAsync(ContentItem item, IReadOnlyDictionary<string, JsonObject?> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var type = await definitions.GetTypeDefinitionAsync(item.ContentType);
        if (type is null)
        {
            return $"Unknown content type '{item.ContentType}'.";
        }

        foreach (var (pathText, value) in values)
        {
            if (value is null)
            {
                continue;
            }

            if (!FieldPath.TryParse(pathText, out var path) || ContentFieldValueCopier.Resolve(type, path) is not { } field)
            {
                return $"Field {pathText} does not exist on {item.ContentType}.";
            }

            fields.WriteField(item, path, field.FieldDefinition.Name, value);
        }

        return null;
    }

    private async Task<string?> ValidateAsync(ContentItem item)
    {
        var result = await contentManager.ValidateAsync(item);
        return result.Succeeded
            ? null
            : $"{item.ContentType} '{item.ContentItemId}' is not valid: {string.Join("; ", result.Errors.Select(error => error.ErrorMessage))}";
    }

    private CallerContext Caller(ContentOperationKind operation, string subject) =>
        callers.Current
            ?? throw new Contexts.WorkflowAccessRefusedException($"{operation} of '{subject}' takes {PermissionOf(operation)} but the burst has no caller.");

    private async Task RequireAsync(CallerContext caller, ContentOperationKind operation, ContentItem item, CancellationToken cancellationToken)
    {
        var permission = PermissionOf(operation);
        var verdict = await decision.DecideAsync(caller, permission, item, cancellationToken);
        if (!verdict.IsAllowed)
        {
            throw new Contexts.WorkflowAccessRefusedException($"'{caller.UserName ?? "anonymous"}' may not {operation.ToString().ToLowerInvariant()} '{item.ContentItemId}' ({item.ContentType}){(verdict.Reason is null ? string.Empty : $": {verdict.Reason}")}.");
        }
    }
}

/// <summary>
/// The one content-operation activity: every content type gets one engine descriptor per
/// operation whose CLR type is this class (<see cref="ContentOperationActivityProvider"/>),
/// with the type's fields as typed synthetic inputs on create and update. The descriptor,
/// never the node, names the type and the operation.
/// </summary>
[Activity("Crest.Workflows", "Content", "Creates, updates, deletes, publishes or unpublishes a content item of one type.", DisplayName = "Content operation")]
[FlowNode("Done", "Failed")]
public class ContentOperation : Activity
{
    /// <summary>The type and operation this node was created for; informational only.</summary>
    public string ContentType { get; set; } = string.Empty;

    public string Operation { get; set; } = string.Empty;

    [Input(DisplayName = "Item id", Description = "The content item to act on. Empty = the trigger payload's ContentItemId.", UIHint = InputUIHints.SingleLine)]
    public Input<string?> ContentItemId { get; set; } = null!;

    [Input(DisplayName = "Display text", Description = "Optional. The item's display text.", UIHint = InputUIHints.SingleLine)]
    public Input<string?> DisplayText { get; set; } = null!;

    [Input(DisplayName = "Publish", Description = "Publish the item as it is created; otherwise it is created as a draft.", UIHint = InputUIHints.Checkbox)]
    public Input<bool> Publish { get; set; } = null!;

    [Output(Description = "The id of the item acted on.")]
    public Output<string?> ItemId { get; set; } = null!;

    [Output(Description = "The item acted on.")]
    public Output<ContentItem?> Item { get; set; } = null!;

    [Output(Description = "Why the operation ended on Failed, when it did.")]
    public Output<string?> Failure { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var descriptor = context.ActivityDescriptor;
        var contentType = descriptor.CustomProperties.TryGetValue(WorkflowsConstants.ContentTypeDescriptorProperty, out var type) ? type as string : null;
        if (string.IsNullOrEmpty(contentType)
            || !descriptor.CustomProperties.TryGetValue(WorkflowsConstants.ContentOperationDescriptorProperty, out var named)
            || !Enum.TryParse<ContentOperationKind>(named?.ToString(), ignoreCase: true, out var operation))
        {
            throw new InvalidOperationException("The content-operation activity names no type or operation; it is created from a content operation descriptor, never by hand.");
        }

        var service = context.GetRequiredService<ContentOperationService>();
        var result = operation == ContentOperationKind.Create
            ? await service.CreateAsync(contentType, DisplayText.GetOrDefault(context), Values(context), Publish.GetOrDefault(context), context.CancellationToken)
            : ItemIdOf(context) is not { } id
                ? ContentOperationResult.Failed("An item id is required.")
                : operation switch
                {
                    ContentOperationKind.Update => await service.UpdateAsync(contentType, id, DisplayText.GetOrDefault(context), Values(context), context.CancellationToken),
                    ContentOperationKind.Delete => await service.DeleteAsync(contentType, id, context.CancellationToken),
                    ContentOperationKind.Publish => await service.PublishAsync(contentType, id, context.CancellationToken),
                    _ => await service.UnpublishAsync(contentType, id, context.CancellationToken),
                };

        ItemId.Set(context, result.Item?.ContentItemId);
        Item.Set(context, result.Item);
        context.JournalData["Operation"] = $"{operation} {contentType}";
        if (!result.Succeeded)
        {
            Failure.Set(context, result.Failure);
            context.JournalData["Error"] = result.Failure;
            await context.CompleteActivityWithOutcomesAsync("Failed");
            return;
        }

        context.JournalData["ContentItemId"] = result.Item?.ContentItemId;
        await context.CompleteActivityWithOutcomesAsync("Done");
    }

    private string? ItemIdOf(ActivityExecutionContext context)
    {
        var id = ContentItemId.GetOrDefault(context);
        if (string.IsNullOrWhiteSpace(id))
        {
            var payload = context.FindWorkflowInput<IDictionary<string, object>>(WorkflowsConstants.InputKeys.Payload);
            id = payload is not null && payload.TryGetValue("ContentItemId", out var value) ? value?.ToString() : null;
        }

        return string.IsNullOrWhiteSpace(id) ? null : id;
    }

    /// <summary>The synthetic field inputs' values as field JSON, keyed Part.Field; unset inputs are left out.</summary>
    private Dictionary<string, JsonObject?> Values(ActivityExecutionContext context)
    {
        var values = new Dictionary<string, JsonObject?>(StringComparer.OrdinalIgnoreCase);
        foreach (var input in context.ActivityDescriptor.Inputs.Where(input => input.IsSynthetic))
        {
            if (!ContentOperationActivityProvider.TryFieldOf(context.ActivityDescriptor, input, out var path, out var fieldType))
            {
                continue;
            }

            var reference = SyntheticProperties.TryGetValue(input.Name, out var value) ? value as Input : null;
            var evaluated = reference is not null ? context.Get(reference.MemoryBlockReference()) : null;
            if (evaluated is not null)
            {
                values[path] = ContentFieldValues.ToJson(fieldType, evaluated);
            }
        }

        return values;
    }
}

/// <summary>The field kinds a content operation takes as typed inputs, and their JSON shapes.</summary>
public static class ContentFieldValues
{
    private static readonly Dictionary<string, (Type ClrType, string Property)> Kinds = new(StringComparer.Ordinal)
    {
        ["TextField"] = (typeof(string), "Text"),
        ["HtmlField"] = (typeof(string), "Html"),
        ["MarkdownField"] = (typeof(string), "Markdown"),
        ["NumericField"] = (typeof(decimal?), "Value"),
        ["BooleanField"] = (typeof(bool?), "Value"),
        ["DateField"] = (typeof(DateTime?), "Value"),
        ["DateTimeField"] = (typeof(DateTime?), "Value"),
        ["ContentPickerField"] = (typeof(IList<string>), "ContentItemIds"),
        ["UserPickerField"] = (typeof(IList<string>), "UserIds"),
        ["OptionPickerField"] = (typeof(IList<string>), "SelectedIds"),
    };

    /// <summary>The CLR type a field kind is set with; null for a kind set only by Copy fields.</summary>
    public static Type? ClrTypeOf(string fieldType) => Kinds.TryGetValue(fieldType, out var kind) ? kind.ClrType : null;

    public static JsonObject? ToJson(string fieldType, object value)
    {
        if (!Kinds.TryGetValue(fieldType, out var kind))
        {
            return null;
        }

        JsonNode? node = value switch
        {
            string text when kind.ClrType == typeof(IList<string>) => new JsonArray(text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(id => (JsonNode?)id).ToArray()),
            IEnumerable<string> ids => new JsonArray(ids.Select(id => (JsonNode?)id).ToArray()),
            JsonNode json => json.DeepClone(),
            _ => JsonValue.Create(Convert(value, kind.ClrType)),
        };

        return new JsonObject { [kind.Property] = node };
    }

    private static object? Convert(object value, Type type)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;
        return target.IsInstanceOfType(value)
            ? value
            : System.Convert.ChangeType(value, target, System.Globalization.CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Emits one <see cref="ActivityDescriptor"/> per content type and operation (the registry's
/// content operations, docs/workflows.md › Tasks › 3): create and update take the type's
/// fields as typed synthetic inputs (<c>Part.Field</c>); every operation carries the gate's
/// permission (the owner variation of the operation's content permission) asked against the
/// type, so the per-type permissions decide. The CLR type is always
/// <see cref="ContentOperation"/>.
/// </summary>
public sealed class ContentOperationActivityProvider(IContentDefinitionManager definitions, IActivityDescriber describer) : IActivityProvider
{
    public const string TypeNamePrefix = "Crest.Content.";
    private const string SyntheticPrefix = "Field_";
    private const string FieldsProperty = "crest:contentFields";

    public async ValueTask<IEnumerable<ActivityDescriptor>> GetDescriptorsAsync(CancellationToken cancellationToken = default)
    {
        var types = await definitions.ListTypeDefinitionsAsync();
        if (!types.Any())
        {
            return [];
        }

        var template = await describer.DescribeActivityAsync(typeof(ContentOperation), cancellationToken);
        return types
            .SelectMany(type => Enum.GetValues<ContentOperationKind>().Select(operation => Describe(type, operation, template)))
            .ToList();
    }

    public static ActivityDescriptor Describe(ContentTypeDefinition type, ContentOperationKind operation, ActivityDescriptor template)
    {
        var name = $"{operation}{SafeName(type.Name)}";
        var typeName = TypeNamePrefix + name;
        var writes = operation is ContentOperationKind.Create or ContentOperationKind.Update;

        var fieldsByInput = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var inputs = template.Inputs.Where(input => input.Name switch
        {
            nameof(ContentOperation.ContentItemId) => operation != ContentOperationKind.Create,
            nameof(ContentOperation.DisplayText) => writes,
            nameof(ContentOperation.Publish) => operation == ContentOperationKind.Create,
            _ => true,
        }).ToList();

        if (writes)
        {
            foreach (var part in type.Parts)
            {
                foreach (var field in part.PartDefinition.Fields)
                {
                    if (ContentFieldValues.ClrTypeOf(field.FieldDefinition.Name) is not { } clrType)
                    {
                        continue;
                    }

                    var path = $"{part.Name}.{field.Name}";
                    var syntheticName = SyntheticPrefix + SafeName(part.Name) + "__" + SafeName(field.Name);
                    var input = new InputDescriptor
                    {
                        Name = syntheticName,
                        DisplayName = field.DisplayName() is { Length: > 0 } display ? display : path,
                        Description = $"{path} ({field.FieldDefinition.Name}). Empty leaves the field as it is.",
                        Type = clrType,
                        IsWrapped = true,
                        IsSynthetic = true,
                        UIHint = clrType == typeof(bool?) ? InputUIHints.Checkbox : InputUIHints.SingleLine,
                        Category = "Fields",
                        ValueGetter = activity => activity.SyntheticProperties.TryGetValue(syntheticName, out var value) ? value : null,
                        ValueSetter = (activity, value) => activity.SyntheticProperties[syntheticName] = value!,
                    };
                    fieldsByInput[syntheticName] = [path, field.FieldDefinition.Name];
                    inputs.Add(input);
                }
            }
        }

        var permission = ContentOperationService.GatePermissionOf(operation);
        return new ActivityDescriptor
        {
            TypeName = typeName,
            ClrType = typeof(ContentOperation),
            Namespace = "Crest.Content",
            Name = name,
            Version = 1,
            DisplayName = $"{operation} {type.DisplayName}",
            Description = $"{operation}s a {type.DisplayName} through the content manager: validated, its handlers run, decided for the item.",
            Category = $"Content: {type.DisplayName}",
            Kind = ActivityKind.Action,
            Inputs = inputs,
            Outputs = template.Outputs.ToList(),
            Attributes = template.Attributes,
            Ports = template.Ports,
            IsBrowsable = true,
            CustomProperties =
            {
                [WorkflowsConstants.ContentTypeDescriptorProperty] = type.Name,
                [WorkflowsConstants.ContentOperationDescriptorProperty] = operation.ToString(),
                [WorkflowsConstants.RequiredPermissionDescriptorProperty] = permission,
                [WorkflowsConstants.PermissionResourceDescriptorProperty] = type.Name,
                [FieldsProperty] = fieldsByInput,
            },
            ConstructionProperties = new Dictionary<string, object>
            {
                [nameof(ContentOperation.ContentType)] = type.Name,
                [nameof(ContentOperation.Operation)] = operation.ToString(),
            },
            Constructor = context =>
            {
                var result = context.CreateActivity<ContentOperation>();
                result.Activity.Type = typeName;
                result.Activity.ContentType = type.Name;
                result.Activity.Operation = operation.ToString();
                return result;
            },
        };
    }

    /// <summary>The field a synthetic input sets, as its descriptor recorded it (path and field type).</summary>
    public static bool TryFieldOf(ActivityDescriptor descriptor, InputDescriptor input, out string path, out string fieldType)
    {
        if (descriptor.CustomProperties.TryGetValue(FieldsProperty, out var value)
            && value is IReadOnlyDictionary<string, string[]> fields
            && fields.TryGetValue(input.Name, out var field) && field.Length == 2)
        {
            path = field[0];
            fieldType = field[1];
            return true;
        }

        path = fieldType = string.Empty;
        return false;
    }

    private static string SafeName(string name) => new(name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
}
