using Crest.Workflows.Abstractions;
using Crest.Workflows.Features.Contracts;
using Crest.Workflows.Features.Models;
using Crest.Workflows.Models;
using JetBrains.Annotations;

namespace Crest.Workflows.Api.Endpoints.Features.List;

/// <summary>
/// Returns a list of installed features.
/// </summary>
[PublicAPI]
internal class List : WorkflowsEndpointWithoutRequest<ListResponse<FeatureDescriptor>>
{
    private readonly IInstalledFeatureRegistry _installedFeatureRegistry;

    /// <inheritdoc />
    public List(IInstalledFeatureRegistry installedFeatureRegistry)
    {
        _installedFeatureRegistry = installedFeatureRegistry;
    }

    /// <inheritdoc />
    public override void Configure()
    {
        Get("/features/installed");
        ConfigurePermissions("read:*", "read:installed-features");
    }

    /// <inheritdoc />
    public override Task<ListResponse<FeatureDescriptor>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var descriptors = _installedFeatureRegistry.List().ToList();
        var response = new ListResponse<FeatureDescriptor>(descriptors);

        return Task.FromResult(response);
    }
}