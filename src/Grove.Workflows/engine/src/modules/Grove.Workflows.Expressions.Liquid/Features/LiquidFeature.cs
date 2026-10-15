using Grove.Workflows.Caching.Features;
using Grove.Workflows.Common.Features;
using Grove.Workflows.Expressions.Features;
using Grove.Workflows.Extensions;
using Grove.Workflows.Features.Abstractions;
using Grove.Workflows.Features.Attributes;
using Grove.Workflows.Features.Services;
using Grove.Workflows.Expressions.Liquid.Contracts;
using Grove.Workflows.Expressions.Liquid.Filters;
using Grove.Workflows.Expressions.Liquid.Handlers;
using Grove.Workflows.Expressions.Liquid.Options;
using Grove.Workflows.Expressions.Liquid.Providers;
using Grove.Workflows.Expressions.Liquid.Services;
using Fluid.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Workflows.Expressions.Liquid.Features;

/// <summary>
/// Configures liquid functionality.
/// </summary>
[DependsOn(typeof(MemoryCacheFeature))]
[DependsOn(typeof(MediatorFeature))]
[DependsOn(typeof(ExpressionsFeature))]
public class LiquidFeature : FeatureBase
{
    /// <inheritdoc />
    public LiquidFeature(IModule serviceConfiguration) : base(serviceConfiguration)
    {
    }

    /// <summary>
    /// Configures the Fluid options.
    /// </summary>
    public Action<FluidOptions> FluidOptions { get; set; } = options =>
    {
        options.ConfigureFilters = context => context.Options.Filters
            .WithArrayFilters()
            .WithStringFilters()
            .WithNumberFilters()
            .WithMiscFilters();
    };

    /// <inheritdoc />
    public override void Apply()
    {
        Services.Configure(FluidOptions);

        Services
            .AddHandlersFrom<ConfigureLiquidEngine>()
            .AddScoped<ILiquidTemplateManager, LiquidTemplateManager>()
            .AddScoped<LiquidParser>()
            .AddExpressionDescriptorProvider<LiquidExpressionDescriptorProvider>()
            .AddLiquidFilter<Base64Filter>("base64")
            .AddLiquidFilter<DictionaryKeysFilter>("keys")
        ;
    }
}