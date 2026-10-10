using Crest.Workflows.Expressions.Contracts;
using Crest.Workflows.Expressions.Services;
using Crest.Workflows.Features.Abstractions;
using Crest.Workflows.Features.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Expressions.Features;

/// <summary>
/// Installs and configures the expressions feature.
/// </summary>
public class ExpressionsFeature : FeatureBase
{
    /// <inheritdoc />
    public ExpressionsFeature(IModule module) : base(module)
    {
    }

    /// <inheritdoc />
    public override void Apply()
    {
        Services
            .AddScoped<IExpressionEvaluator, ExpressionEvaluator>()
            .AddSingleton<IWellKnownTypeRegistry, WellKnownTypeRegistry>();
    }
}