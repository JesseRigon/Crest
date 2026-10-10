using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Crest.Workflows.Abstractions;
using Crest.Workflows.Extensions;
using Crest.Workflows.Models;
using Humanizer;

namespace Crest.Workflows.Api.Endpoints.IncidentStrategies.List;

/// <summary>
/// Returns list of available <see cref="IIncidentStrategy" /> implementations.
/// </summary>
internal class List(IEnumerable<IIncidentStrategy> strategies) : CrestWorkflowsEndpointWithoutRequest<ListResponse<IncidentStrategyDescriptor>>
{
    public override void Configure()
    {
        Get("/descriptors/incident-strategies");
        ConfigurePermissions("read:incident-strategies");
    }

    public override Task<ListResponse<IncidentStrategyDescriptor>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var descriptors = strategies.Select(IncidentStrategyDescriptor.FromStrategy).OrderBy(x => x.DisplayName).ToList();
        var response =new ListResponse<IncidentStrategyDescriptor>(descriptors);
        return Task.FromResult(response);
    }
}

internal record IncidentStrategyDescriptor(string DisplayName, string Description, string TypeName)
{
    public static IncidentStrategyDescriptor FromStrategy(IIncidentStrategy strategy)
    {
        var type = strategy.GetType();
        var displayNameAttribute = type.GetCustomAttribute<DisplayNameAttribute>();
        var descriptionAttribute = type.GetCustomAttribute<DescriptionAttribute>();
        var displayAttribute = type.GetCustomAttribute<DisplayAttribute>();
        var displayName = displayNameAttribute?.DisplayName ?? displayAttribute?.Name ?? type.Name.Replace("Strategy", "").Humanize();
        var description = descriptionAttribute?.Description ?? displayAttribute?.Description ?? "";

        return new IncidentStrategyDescriptor(displayName, description, type.GetSimpleAssemblyQualifiedName());
    }
}