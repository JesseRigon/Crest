using Crest.Workflows.Caching.Features;
using Crest.Workflows.Common.Features;
using Crest.Workflows.Expressions.Features;
using Crest.Workflows.Extensions;
using Crest.Workflows.Features.Abstractions;
using Crest.Workflows.Features.Attributes;
using Crest.Workflows.Features.Services;
using Crest.Workflows.Expressions.Liquid.Contracts;
using Crest.Workflows.Expressions.Liquid.Filters;
using Crest.Workflows.Expressions.Liquid.Handlers;
using Crest.Workflows.Expressions.Liquid.Options;
using Crest.Workflows.Expressions.Liquid.Providers;
using Crest.Workflows.Expressions.Liquid.Services;
using Fluid.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Expressions.Liquid.Features;

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