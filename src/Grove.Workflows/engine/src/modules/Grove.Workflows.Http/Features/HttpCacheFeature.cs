using Grove.Workflows.Features.Abstractions;
using Grove.Workflows.Features.Attributes;
using Grove.Workflows.Features.Services;
using Grove.Workflows.Http.Handlers;
using Grove.Workflows.Http.Services;
using Grove.Workflows.Management.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Workflows.Http.Features;

/// <summary>
/// Installs services related to HTTP caching.
/// </summary>
[DependsOn(typeof(HttpFeature))]
[DependsOn(typeof(CachingWorkflowDefinitionsFeature))]
public class HttpCacheFeature : FeatureBase
{
    /// <inheritdoc />
    public HttpCacheFeature(IModule module) : base(module)
    {
    }

    /// <inheritdoc />
    public override void Apply()
    {
        Services
            .AddSingleton<IHttpWorkflowsCacheManager, HttpWorkflowsCacheManager>()
            .Decorate<IHttpWorkflowLookupService, CachingHttpWorkflowLookupService>()
            .AddNotificationHandler<InvalidateHttpWorkflowsCache>();
    }
}