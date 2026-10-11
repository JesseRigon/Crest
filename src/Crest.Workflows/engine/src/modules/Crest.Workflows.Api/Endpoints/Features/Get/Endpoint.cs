using Crest.Workflows.Abstractions;
using Crest.Workflows.Features.Contracts;
using Crest.Workflows.Features.Models;
using JetBrains.Annotations;

namespace Crest.Workflows.Api.Endpoints.Features.Get;

/// <summary>
/// Returns the specified installed feature.
/// </summary>
[PublicAPI]
internal class Get : WorkflowsEndpointWithoutRequest<FeatureDescriptor>
{
    private readonly IInstalledFeatureRegistry _installedFeatureRegistry;

    /// <inheritdoc />
    public Get(IInstalledFeatureRegistry installedFeatureRegistry)
    {
        _installedFeatureRegistry = installedFeatureRegistry;
    }

    /// <inheritdoc />
    public override void Configure()
    {
        Get("/features/installed/{fullName}");
        ConfigurePermissions("read:*", "read:installed-features");
    }

    /// <inheritdoc />
    public override async Task HandleAsync( CancellationToken cancellationToken)
    {
        var fullName = Route<string>("fullName")!;
        var descriptor = _installedFeatureRegistry.Find(fullName);

        if (descriptor == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }
        
        await Send.OkAsync(descriptor, cancellationToken);
    }
}