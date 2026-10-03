using Crest.Workflows.Features.Abstractions;
using Crest.Workflows.Features.Attributes;
using Crest.Workflows.Features.Services;
using Crest.Workflows.Http.Handlers;
using Crest.Workflows.Http.Services;
using Crest.Workflows.Management.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Http.Features;

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