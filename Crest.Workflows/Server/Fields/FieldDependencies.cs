using System.Text.Json.Nodes;
using VersionOptions = Crest.Workflows.Common.Models.VersionOptions;
using PlatformVersionOptions = Crest.ContentManagement.VersionOptions;
using Crest.Workflows.Contents;
using Crest.Workflows.Management;
using Crest.Workflows.Management.Activities.WorkflowDefinitionActivity;
using Crest.Workflows.Management.Models;
using Crest.Workflows.Management.Notifications;
using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Models;
using Crest.Workflows.Activities;
using Microsoft.Extensions.Logging;
using Crest.ContentManagement;
using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Metadata.Models;

namespace Crest.Workflows.Fields;

/// <summary>Everything an activity type or instance declares, from attributes and from its bindings.</summary>
public static class FieldDependencyDeclarations
{
    public static IReadOnlyList<WorkflowFieldDependencyDescriptor> Of(Type activityType) =>
        activityType.GetCustomAttributes(typeof(FieldDependencyAttribute), inherit: true).Cast<FieldDependencyAttribute>().Select(a => a.ToDescriptor()).ToArray();

    public static IReadOnlyList<WorkflowFieldDependencyDescriptor> Of(IActivity activity)
    {
        var declared = Of(activity.GetType()).ToList();
        if (activity is IFieldDependencySource source)
        {
            declared.AddRange(source.GetFieldDependencies());
        }

        return declared;
    }
}

/// <summary>Adds the class-declared dependencies to the engine's activity descriptor, where the designer's palette and the registry read them.</summary>
public sealed class FieldDependencyDescriptorModifier : IActivityDescriptorModifier
{
    public void Modify(ActivityDescriptor descriptor)
    {
        var dependencies = FieldDependencyDeclarations.Of(descriptor.ClrType);
        if (dependencies.Count > 0)
        {
            descriptor.CustomProperties[WorkflowsConstants.FieldDependenciesDescriptorProperty] = dependencies;
        }
    }
}

/// <summary>
/// Resolves a flow's field dependencies against the tenant's content definitions as they
/// stand - at publish (warnings stamped on the version, never a refusal) and on demand for
/// the designer. Nested published definitions are walked like the atomicity analyzer does.
/// A path with no content type is checked against every type carrying the part.
/// </summary>
public sealed class WorkflowFieldDependencyAnalyzer(IWorkflowDefinitionService definitionService, IWorkflowGraphBuilder graphBuilder, IContentDefinitionManager definitions)
{
    public async Task<WorkflowFieldDependencyReport?> AnalyzeAsync(string definitionId, CancellationToken cancellationToken = default)
    {
        var graph = await definitionService.FindWorkflowGraphAsync(definitionId, VersionOptions.Latest, cancellationToken);
        return graph is null ? null : await AnalyzeAsync(graph, definitionId, cancellationToken);
    }

    public async Task<WorkflowFieldDependencyReport> AnalyzeAsync(Workflow workflow, CancellationToken cancellationToken = default) =>
        await AnalyzeAsync(await graphBuilder.BuildAsync(workflow, cancellationToken), workflow.Identity.DefinitionId, cancellationToken);

    private async Task<WorkflowFieldDependencyReport> AnalyzeAsync(WorkflowGraph graph, string definitionId, CancellationToken cancellationToken)
    {
        var types = (await definitions.ListTypeDefinitionsAsync()).ToList();
        var bindings = new List<WorkflowFieldDependencyBinding>();
        await WalkAsync(graph, null, types, bindings, new HashSet<string>(StringComparer.Ordinal) { definitionId }, cancellationToken);

        var warnings = bindings.Where(b => b.Status != WorkflowFieldDependencyStatuses.Ok && (b.Required || b.Status is WorkflowFieldDependencyStatuses.MissingPart or WorkflowFieldDependencyStatuses.MissingField))
            .Select(b => new WorkflowFieldDependencyWarning(b.ActivityId, b.ActivityType, b.Path, b.Status, Describe(b), b.InDefinition))
            .ToList();
        return new(bindings, warnings);
    }

    private async Task WalkAsync(WorkflowGraph graph, string? inDefinition, List<ContentTypeDefinition> types, List<WorkflowFieldDependencyBinding> bindings, HashSet<string> visited, CancellationToken cancellationToken)
    {
        foreach (var node in graph.Nodes)
        {
            var activity = node.Activity;
            if (activity is Workflow)
            {
                continue;
            }

            foreach (var dependency in FieldDependencyDeclarations.Of(activity))
            {
                bindings.Add(Resolve(activity, dependency, types, inDefinition));
            }

            if (activity is WorkflowDefinitionActivity nested && !string.IsNullOrEmpty(nested.WorkflowDefinitionId) && visited.Add(nested.WorkflowDefinitionId))
            {
                var inner = await definitionService.FindWorkflowGraphAsync(nested.WorkflowDefinitionId, VersionOptions.Published, cancellationToken);
                if (inner is not null)
                {
                    await WalkAsync(inner, nested.WorkflowDefinitionId, types, bindings, visited, cancellationToken);
                }
            }
        }
    }

    public static WorkflowFieldDependencyBinding Resolve(IActivity activity, WorkflowFieldDependencyDescriptor dependency, IReadOnlyList<ContentTypeDefinition> types, string? inDefinition)
    {
        WorkflowFieldDependencyBinding Binding(string status, string? fieldType = null) => new(activity.Id, activity.Type, dependency.Path, dependency.Required, dependency.Writes, dependency.ContentType, status, fieldType, inDefinition);

        if (!FieldPath.TryParse(dependency.Path, out var path))
        {
            return Binding(WorkflowFieldDependencyStatuses.MissingPart);
        }

        var candidates = (dependency.ContentType is null ? types : types.Where(t => string.Equals(t.Name, dependency.ContentType, StringComparison.Ordinal)))
            .Select(t => t.Parts.FirstOrDefault(p => string.Equals(p.Name, path.Part, StringComparison.OrdinalIgnoreCase)))
            .Where(p => p is not null)
            .Select(p => p!)
            .ToList();
        if (candidates.Count == 0)
        {
            return Binding(WorkflowFieldDependencyStatuses.MissingPart);
        }

        var fields = candidates.Select(p => p.PartDefinition.Fields.FirstOrDefault(f => string.Equals(f.Name, path.Field, StringComparison.OrdinalIgnoreCase))).Where(f => f is not null).Select(f => f!).ToList();
        if (fields.Count == 0)
        {
            return Binding(WorkflowFieldDependencyStatuses.MissingField);
        }

        // A written field only has to exist. For a read one, several types may carry the
        // part: the field counts as required only when every one requires it.
        var fieldType = fields[0].FieldDefinition.Name;
        if (dependency.Writes)
        {
            return Binding(WorkflowFieldDependencyStatuses.Ok, fieldType);
        }

        if (dependency.Required && fields.Any(f => !IsRequired(f)))
        {
            return Binding(WorkflowFieldDependencyStatuses.Optional, fieldType);
        }

        if (dependency.Required && fields.Any(HasVisibilityCondition))
        {
            return Binding(WorkflowFieldDependencyStatuses.Conditional, fieldType);
        }

        return Binding(WorkflowFieldDependencyStatuses.Ok, fieldType);
    }

    /// <summary>Crest keeps Required on the field's typed settings (<c>TextFieldSettings.Required</c>, ...).</summary>
    private static bool IsRequired(ContentPartFieldDefinition field) =>
        field.Settings[$"{field.FieldDefinition.Name}Settings"] is JsonObject settings && settings["Required"]?.GetValue<bool>() == true;

    /// <summary>Crest's conditional visibility (<c>CrestFieldVisibilitySettings.Path</c> set) means the field may be hidden for some items.</summary>
    private static bool HasVisibilityCondition(ContentPartFieldDefinition field) =>
        field.Settings["CrestFieldVisibilitySettings"] is JsonObject settings && !string.IsNullOrWhiteSpace(settings["Path"]?.GetValue<string>());

    private static string Describe(WorkflowFieldDependencyBinding b) => b.Status switch
    {
        WorkflowFieldDependencyStatuses.MissingPart => $"{b.Path}: no content type{(b.ContentType is null ? string.Empty : $" '{b.ContentType}'")} carries a part named '{b.Path.Split('.')[0]}'.",
        WorkflowFieldDependencyStatuses.MissingField => $"{b.Path}: the part has no field '{b.Path.Split('.').Last()}'.",
        WorkflowFieldDependencyStatuses.Optional => $"{b.Path}: the activity requires it but the field is optional in the tenant's definition; a run with it empty ends on Failed.",
        WorkflowFieldDependencyStatuses.Conditional => $"{b.Path}: the activity requires it but the field is shown only under a condition; a run with it empty ends on Failed.",
        _ => b.Path,
    };
}

/// <summary>
/// Publish stamps the warnings on the version being published (<see cref="WorkflowsConstants.FieldWarningsProperty"/>)
/// and logs them; it never refuses. A tenant who wants a run to stop on a condition adds the
/// condition node - the warning is the reminder.
/// </summary>
public sealed class FieldDependencyPublishHandler(WorkflowFieldDependencyAnalyzer analyzer, IWorkflowDefinitionService definitionService, ILogger<FieldDependencyPublishHandler> logger) : INotificationHandler<WorkflowDefinitionPublishing>
{
    public async Task HandleAsync(WorkflowDefinitionPublishing notification, CancellationToken cancellationToken)
    {
        var definition = notification.WorkflowDefinition;
        var graph = await definitionService.MaterializeWorkflowAsync(definition, cancellationToken);
        var report = await analyzer.AnalyzeAsync(graph.Workflow, cancellationToken);

        if (report.Warnings.Count == 0)
        {
            definition.CustomProperties.Remove(WorkflowsConstants.FieldWarningsProperty);
            return;
        }

        definition.CustomProperties[WorkflowsConstants.FieldWarningsProperty] = report.Warnings;
        foreach (var warning in report.Warnings)
        {
            logger.LogWarning("Workflow '{Name}' published with a field warning on {Activity}: {Message}", definition.Name, warning.ActivityId, warning.Message);
        }
    }
}

/// <inheritdoc cref="IWorkflowFieldDependencyChecker"/>
public sealed class FieldDependencyChecker(IContentDefinitionManager definitions, IContentManager contentManager) : IWorkflowFieldDependencyChecker
{
    public async Task<string?> CheckAsync(object activity, string contentItemId, CancellationToken cancellationToken = default)
    {
        var item = await contentManager.GetAsync(contentItemId, PlatformVersionOptions.Latest);
        return item is null ? null : await CheckAsync(activity, item);
    }

    public async Task<string?> CheckAsync(object activity, ContentItem item)
    {
        var type = await definitions.GetTypeDefinitionAsync(item.ContentType);
        var declared = activity is IActivity engineActivity ? FieldDependencyDeclarations.Of(engineActivity) : FieldDependencyDeclarations.Of(activity.GetType());
        foreach (var dependency in declared.Where(d => d.Required))
        {
            if (dependency.ContentType is not null && !string.Equals(dependency.ContentType, item.ContentType, StringComparison.Ordinal))
            {
                continue;
            }

            if (!FieldPath.TryParse(dependency.Path, out var path))
            {
                continue;
            }

            var field = type is null ? null : ContentFieldValueCopier.Resolve(type, path);
            if (field is null)
            {
                return $"Required field {path} does not exist on {item.ContentType}.";
            }

            if (ContentFieldValueCopier.IsEmpty(field.FieldDefinition.Name, ContentFieldValueCopier.Read(item, path)))
            {
                return $"Required field {path} is empty on {item.ContentType} '{item.ContentItemId}'.";
            }
        }

        return null;
    }
}
