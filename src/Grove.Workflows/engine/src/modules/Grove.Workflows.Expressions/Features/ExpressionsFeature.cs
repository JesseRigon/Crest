using Grove.Workflows.Expressions.Contracts;
using Grove.Workflows.Expressions.Services;
using Grove.Workflows.Features.Abstractions;
using Grove.Workflows.Features.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Workflows.Expressions.Features;

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