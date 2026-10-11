using System.ComponentModel;
using System.Reflection;
using Crest.Workflows.Abstractions;
using Crest.Workflows.Expressions.Contracts;
using Crest.Workflows.Extensions;
using Crest.Workflows.Management.Models;
using Crest.Workflows.Management.Options;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;

namespace Crest.Workflows.Api.Endpoints.VariableTypes.List;

/// <summary>
/// Returns a list of available variable types.
/// </summary>
[PublicAPI]
internal class List : WorkflowsEndpointWithoutRequest<Response>
{
    private readonly IWellKnownTypeRegistry _wellKnownTypeRegistry;
    private readonly ManagementOptions _managementOptions;

    /// <inheritdoc />
    public List(IOptions<ManagementOptions> managementOptions, IWellKnownTypeRegistry wellKnownTypeRegistry)
    {
        _wellKnownTypeRegistry = wellKnownTypeRegistry;
        _managementOptions = managementOptions.Value;
    }

    /// <inheritdoc />
    public override void Configure()
    {
        Get("/descriptors/variables");
        ConfigurePermissions("read:*", "read:variable-descriptors");
    }

    /// <inheritdoc />
    public override Task<Response> ExecuteAsync(CancellationToken cancellationToken)
    {
        var types = _managementOptions.VariableDescriptors;
        var descriptors = types.Select(Map).ToList();
        var response = new Response(descriptors);

        return Task.FromResult(response);
    }

    private VariableTypeDescriptor Map(VariableDescriptor descriptor)
    {
        var type = descriptor.Type;
        var hasAlias = _wellKnownTypeRegistry.TryGetAlias(type, out var alias);
        var typeName = hasAlias ? alias : type.GetSimpleAssemblyQualifiedName()!;
        var displayName = hasAlias ? alias : type.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? type.Name;
        var description = descriptor.Description ?? type.GetCustomAttribute<DescriptionAttribute>()?.Description;

        return new(typeName, displayName, descriptor.Category, description);
    }
}