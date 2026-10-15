using Grove.Workflows.Features.Abstractions;
using Grove.Workflows.Features.Attributes;
using Grove.Workflows.Features.Services;
using Grove.Workflows.Http.Scripting.JavaScript;
using Grove.Workflows.Expressions.JavaScript.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Workflows.Http.Features;

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