using Crest.Workflows.Extensions;
using Crest.Workflows.Features.Abstractions;
using Crest.Workflows.Features.Attributes;
using Crest.Workflows.Features.Services;

namespace Crest.Workflows.Tenants.Features;

/// <summary>
/// Enables tenant management endpoints.
/// </summary>
[DependsOn(typeof(TenantManagementFeature))]
public class TenantManagementEndpointsFeature(IModule serviceConfiguration) : FeatureBase(serviceConfiguration)
{
    /// <inheritdoc />
    public override void Configure()
    {
        Module.AddFastEndpointsAssembly<TenantManagementEndpointsFeature>();
    }
}