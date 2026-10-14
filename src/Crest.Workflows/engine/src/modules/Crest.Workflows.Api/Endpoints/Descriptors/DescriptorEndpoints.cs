using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;
using Crest.Workflows.CommitStates;
using Crest.Workflows.Common.Models;
using Crest.Workflows.Expressions.Contracts;
using Crest.Workflows.Expressions.JavaScript.TypeDefinitions.Contracts;
using Crest.Workflows.Expressions.JavaScript.TypeDefinitions.Models;
using Crest.Workflows.Extensions;
using Crest.Workflows.LogPersistence;
using Crest.Workflows.Management;
using Crest.Workflows.Management.Models;
using Crest.Workflows.Management.Options;
using Crest.Workflows.Models;
using Humanizer;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Crest.Workflows.Api.Endpoints.Descriptors;

/// <summary>The descriptor reads: activities and their options, strategies, drivers, variable types, expressions.</summary>
public static class DescriptorEndpoints
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/descriptors/activities", ListActivitiesAsync).RequireAuthorization(EngineApiPolicies.ReadActivityDescriptors);
        routes.MapGet("/descriptors/activities/{typeName}", GetActivityAsync).RequireAuthorization(EngineApiPolicies.ReadActivityDescriptors);
        routes.MapPost("/descriptors/activities/{activityTypeName}/options/{propertyName}", GetActivityOptionsAsync).RequireAuthorization(EngineApiPolicies.ReadActivityDescriptorOptions);
        routes.MapGet("/descriptors/commit-strategies/activities", ListActivityCommitStrategies).RequireAuthorization(EngineApiPolicies.ReadCommitStrategies);
        routes.MapGet("/descriptors/commit-strategies/workflows", ListWorkflowCommitStrategies).RequireAuthorization(EngineApiPolicies.ReadCommitStrategies);
        routes.MapGet("/descriptors/incident-strategies", ListIncidentStrategies).RequireAuthorization(EngineApiPolicies.ReadIncidentStrategies);
        routes.MapGet("/descriptors/log-persistence-strategies", ListLogPersistenceStrategies).RequireAuthorization(EngineApiPolicies.ReadLogPersistenceStrategies);
        routes.MapGet("/descriptors/workflow-activation-strategies", ListWorkflowActivationStrategies).RequireAuthorization(EngineApiPolicies.ReadWorkflowActivationStrategies);
        routes.MapGet("/descriptors/storage-drivers", ListStorageDrivers).RequireAuthorization(EngineApiPolicies.ReadStorageDrivers);
        routes.MapGet("/descriptors/variables", ListVariableTypes).RequireAuthorization(EngineApiPolicies.ReadVariableDescriptors);
        routes.MapGet("/descriptors/expression-descriptors", ListExpressionDescriptors).RequireAuthorization(EngineApiPolicies.ReadExpressionDescriptors);
        routes.MapPost("/scripting/javascript/type-definitions/{workflowDefinitionId}", GetJavaScriptTypeDefinitionsAsync).RequireAuthorization(EngineApiPolicies.ReadJavaScriptTypeDefinitions);
    }

    private static async Task<IResult> ListActivitiesAsync(
        HttpContext context,
        [FromQuery] bool? refresh,
        [FromServices] IActivityRegistry registry,
        [FromServices] IActivityRegistryPopulator registryPopulator,
        CancellationToken cancellationToken)
    {
        if (refresh == true)
        {
            await registryPopulator.PopulateRegistryAsync(cancellationToken);
        }

        return EngineResults.Json(context, new ActivityDescriptorsResponse(registry.ListAll().ToList()));
    }

    private static async Task<IResult> GetActivityAsync(
        HttpContext context,
        [FromRoute] string typeName,
        [FromQuery] int? version,
        [FromServices] IActivityRegistryLookupService registryLookup)
    {
        var descriptor = version == null ? await registryLookup.FindAsync(typeName) : await registryLookup.FindAsync(typeName, version.Value);
        return descriptor == null ? EngineResults.NotFound() : EngineResults.Json(context, descriptor);
    }

    private static async Task<IResult> GetActivityOptionsAsync(
        HttpContext context,
        [FromRoute] string activityTypeName,
        [FromRoute] string propertyName,
        EngineBody<ActivityDescriptorOptionsRequest> body,
        [FromServices] IActivityRegistryLookupService registryLookup,
        [FromServices] IPropertyUIHandlerResolver optionsResolver,
        CancellationToken cancellationToken)
    {
        var request = body.Value;
        var descriptor = request.Version == null ? await registryLookup.FindAsync(activityTypeName) : await registryLookup.FindAsync(activityTypeName, request.Version.Value);
        var propertyDescriptor = descriptor?.Inputs.FirstOrDefault(x => x.Name == propertyName);
        if (propertyDescriptor == null)
        {
            return EngineResults.NotFound();
        }

        var inputOptions = await optionsResolver.GetUIPropertiesAsync(propertyDescriptor.PropertyInfo!, request.Context, cancellationToken);
        return EngineResults.Json(context, new ActivityDescriptorOptionsResponse(inputOptions));
    }

    private static IResult ListActivityCommitStrategies(HttpContext context, [FromServices] ICommitStrategyRegistry registry)
    {
        var descriptors = CommitStrategyDescriptor.FromStrategyMetadata(registry.ListActivityStrategyRegistrations().Select(x => x.Metadata)).ToList();
        return EngineResults.Json(context, new ListResponse<CommitStrategyDescriptor>(descriptors));
    }

    private static IResult ListWorkflowCommitStrategies(HttpContext context, [FromServices] ICommitStrategyRegistry registry)
    {
        var descriptors = CommitStrategyDescriptor.FromStrategyMetadata(registry.ListWorkflowStrategyRegistrations().Select(x => x.Metadata)).ToList();
        return EngineResults.Json(context, new ListResponse<CommitStrategyDescriptor>(descriptors));
    }

    private static IResult ListIncidentStrategies(HttpContext context, [FromServices] IEnumerable<IIncidentStrategy> strategies)
    {
        var descriptors = strategies.Select(x => StrategyDescriptor.FromStrategy(x, "Strategy")).OrderBy(x => x.DisplayName).ToList();
        return EngineResults.Json(context, new ListResponse<StrategyDescriptor>(descriptors));
    }

    private static IResult ListLogPersistenceStrategies(HttpContext context, [FromServices] ILogPersistenceStrategyService logPersistenceStrategyService)
    {
        var descriptors = logPersistenceStrategyService.ListStrategies().Select(x => StrategyDescriptor.FromStrategy(x, "Strategy")).OrderBy(x => x.DisplayName).ToList();
        return EngineResults.Json(context, new ListResponse<StrategyDescriptor>(descriptors));
    }

    private static IResult ListWorkflowActivationStrategies(HttpContext context, [FromServices] IEnumerable<IWorkflowActivationStrategy> strategies)
    {
        var descriptors = strategies.Select(x => StrategyDescriptor.FromStrategy(x, "Strategy")).OrderBy(x => x.DisplayName).ToList();
        return EngineResults.Json(context, new ListResponse<StrategyDescriptor>(descriptors));
    }

    private static IResult ListStorageDrivers(HttpContext context, [FromServices] IStorageDriverManager registry)
    {
        var descriptors = registry.List().Select(StorageDriverDescriptor.FromDriver).OrderByDescending(x => x.Priority).ToList();
        return EngineResults.Json(context, new StorageDriversResponse(descriptors));
    }

    private static IResult ListVariableTypes(HttpContext context, [FromServices] IOptions<ManagementOptions> managementOptions, [FromServices] IWellKnownTypeRegistry wellKnownTypeRegistry)
    {
        var descriptors = managementOptions.Value.VariableDescriptors.Select(descriptor => VariableTypeDescriptor.FromDescriptor(descriptor, wellKnownTypeRegistry)).ToList();
        return EngineResults.Json(context, new VariableTypesResponse(descriptors));
    }

    private static IResult ListExpressionDescriptors(HttpContext context, [FromServices] IExpressionDescriptorRegistry expressionDescriptorRegistry)
    {
        var models = expressionDescriptorRegistry.ListAll()
            .Select(descriptor => new ExpressionDescriptorModel(descriptor.Type, descriptor.DisplayName, descriptor.IsSerializable, descriptor.IsBrowsable, descriptor.Properties))
            .ToList();
        return EngineResults.Json(context, new ListResponse<ExpressionDescriptorModel>(models));
    }

    /// <summary>A TypeScript definition for the Monaco editor's intellisense on JavaScript expressions.</summary>
    private static async Task<IResult> GetJavaScriptTypeDefinitionsAsync(
        [FromRoute] string workflowDefinitionId,
        EngineBody<JavaScriptTypeDefinitionsRequest> body,
        [FromServices] ITypeDefinitionService typeDefinitionService,
        [FromServices] IWorkflowDefinitionService workflowDefinitionService,
        CancellationToken cancellationToken)
    {
        var workflowGraph = await workflowDefinitionService.FindWorkflowGraphAsync(workflowDefinitionId, VersionOptions.Latest, cancellationToken);
        if (workflowGraph == null)
        {
            return EngineResults.Errors($"Workflow definition {workflowDefinitionId} not found");
        }

        var typeDefinitionContext = new TypeDefinitionContext(workflowGraph, body.Value.ActivityTypeName, body.Value.PropertyName, cancellationToken);
        var typeDefinitions = await typeDefinitionService.GenerateTypeDefinitionsAsync(typeDefinitionContext);
        return EngineResults.File(Encoding.UTF8.GetBytes(typeDefinitions), $"crestWorkflows.{workflowDefinitionId}.d.ts", "application/x-typescript");
    }
}

[PublicAPI]
public class ActivityDescriptorsResponse(ICollection<ActivityDescriptor> items)
{
    public ICollection<ActivityDescriptor> Items { get; set; } = items;

    public int Count => Items.Count;
}

public class ActivityDescriptorOptionsRequest
{
    public int? Version { get; set; }

    public object? Context { get; set; }
}

[PublicAPI]
public class ActivityDescriptorOptionsResponse(IDictionary<string, object> items)
{
    public IDictionary<string, object> Items { get; set; } = items;
}

public record CommitStrategyDescriptor(string Name, string DisplayName, string Description)
{
    public static IEnumerable<CommitStrategyDescriptor> FromStrategyMetadata(IEnumerable<CommitStrategyMetadata> metadata) =>
        metadata.Select(x => new CommitStrategyDescriptor(x.Name, x.DisplayName, x.Description));
}

/// <summary>A strategy implementation (incident, log persistence, workflow activation) described by its attributes.</summary>
public record StrategyDescriptor(string DisplayName, string Description, string TypeName)
{
    public static StrategyDescriptor FromStrategy(object strategy, string suffix)
    {
        var type = strategy.GetType();
        var displayNameAttribute = type.GetCustomAttribute<DisplayNameAttribute>();
        var descriptionAttribute = type.GetCustomAttribute<DescriptionAttribute>();
        var displayAttribute = type.GetCustomAttribute<DisplayAttribute>();
        var displayName = displayNameAttribute?.DisplayName ?? displayAttribute?.Name ?? type.Name.Replace(suffix, "").Humanize();
        var description = descriptionAttribute?.Description ?? displayAttribute?.Description ?? "";
        return new StrategyDescriptor(displayName, description, type.GetSimpleAssemblyQualifiedName());
    }
}

public class StorageDriversResponse(ICollection<StorageDriverDescriptor> items)
{
    public ICollection<StorageDriverDescriptor> Items { get; set; } = items;
}

public record StorageDriverDescriptor(string TypeName, string DisplayName, double Priority = 0, bool Deprecated = false)
{
    public static StorageDriverDescriptor FromDriver(IStorageDriver driver)
    {
        var type = driver.GetType();
        var deprecated = type.GetCustomAttribute<ObsoleteAttribute>() != null;
        var displayName = type.GetCustomAttribute<DisplayAttribute>()?.Name ?? type.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? type.Name.Replace("StorageDriver", "");
        return new StorageDriverDescriptor(type.GetSimpleAssemblyQualifiedName(), displayName, driver.Priority, deprecated);
    }
}

public class VariableTypesResponse(ICollection<VariableTypeDescriptor> items)
{
    public ICollection<VariableTypeDescriptor> Items { get; set; } = items;
}

public record VariableTypeDescriptor(string TypeName, string DisplayName, string Category, string? Description)
{
    public static VariableTypeDescriptor FromDescriptor(VariableDescriptor descriptor, IWellKnownTypeRegistry wellKnownTypeRegistry)
    {
        var type = descriptor.Type;
        var hasAlias = wellKnownTypeRegistry.TryGetAlias(type, out var alias);
        var typeName = hasAlias ? alias : type.GetSimpleAssemblyQualifiedName()!;
        var displayName = hasAlias ? alias : type.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? type.Name;
        var description = descriptor.Description ?? type.GetCustomAttribute<DescriptionAttribute>()?.Description;
        return new(typeName, displayName, descriptor.Category, description);
    }
}

public record ExpressionDescriptorModel(string Type, string DisplayName, bool IsSerializable, bool IsBrowsable, IDictionary<string, object> Properties);

public class JavaScriptTypeDefinitionsRequest
{
    public string? ActivityTypeName { get; set; }

    public string? PropertyName { get; set; }
}
