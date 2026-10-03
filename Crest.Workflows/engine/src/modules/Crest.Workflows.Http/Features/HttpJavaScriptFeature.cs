using Crest.Workflows.Features.Abstractions;
using Crest.Workflows.Features.Attributes;
using Crest.Workflows.Features.Services;
using Crest.Workflows.Http.Scripting.JavaScript;
using Crest.Workflows.Expressions.JavaScript.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Http.Features;

/// <summary>
/// Enabled when both <see cref="HttpFeature"/> and <see cref="JavaScriptFeature"/> are enabled.
/// </summary>
[DependencyOf(typeof(HttpFeature))]
[DependencyOf(typeof(JavaScriptFeature))]
public class HttpJavaScriptFeature : FeatureBase
{
    /// <inheritdoc />
    public HttpJavaScriptFeature(IModule module) : base(module)
    {
    }

    /// <inheritdoc />
    public override void Apply()
    {
        Services.AddNotificationHandler<HttpJavaScriptHandler>();
    }
}