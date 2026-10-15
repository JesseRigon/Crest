using Grove.Workflows.Expressions.Options;
using Grove.Workflows.Extensions;
using Grove.Workflows.Features.Abstractions;
using Grove.Workflows.Features.Services;
using Grove.Workflows.Resilience.Entities;
using Grove.Workflows.Resilience.Modifiers;
using Grove.Workflows.Resilience.Options;
using Grove.Workflows.Resilience.Recorders;
using Grove.Workflows.Resilience.Serialization;
using Grove.Workflows.Resilience.StrategySources;
using Grove.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Workflows.Resilience.Features;

public class ResilienceFeature(IModule module) : FeatureBase(module)
{
    private Func<IServiceProvider, IRetryAttemptRecorder> _retryAttemptRecorder = sp => sp.GetRequiredService<ActivityExecutionContextRetryAttemptRecorder>();
    private Func<IServiceProvider, IRetryAttemptReader> _retryAttemptReader = sp => sp.GetRequiredService<ActivityExecutionContextRetryAttemptReader>();

    public ResilienceFeature AddResilienceStrategyType<T>() where T : IResilienceStrategy
    {
        return AddResilienceStrategyType(typeof(T));
    }

    public ResilienceFeature AddResilienceStrategyType(Type strategyType)
    {
        Services.Configure<ResilienceOptions>(options => options.StrategyTypes.Add(strategyType));
        return this;
    }

    public ResilienceFeature WithActivityExecutionContextRetryAttemptRecorder()
    {
        return WithRetryAttemptRecorder<ActivityExecutionContextRetryAttemptRecorder>()
            .WithRetryAttemptReader<ActivityExecutionContextRetryAttemptReader>();
    }

    public ResilienceFeature WithVoidRetryAttemptRecorder()
    {
        return WithRetryAttemptRecorder<VoidRetryAttemptRecorder>()
            .WithRetryAttemptReader<VoidRetryAttemptReader>();
    }

    public ResilienceFeature WithRetryAttemptRecorder<T>() => WithRetryAttemptRecorder(sp => (IRetryAttemptRecorder)ActivatorUtilities.CreateInstance<T>(sp)!);
    public ResilienceFeature WithRetryAttemptReader<T>() => WithRetryAttemptReader(sp => (IRetryAttemptReader)ActivatorUtilities.CreateInstance<T>(sp)!);

    public ResilienceFeature WithRetryAttemptRecorder(Func<IServiceProvider, IRetryAttemptRecorder> recorder)
    {
        _retryAttemptRecorder = recorder;
        return this;
    }

    public ResilienceFeature WithRetryAttemptReader(Func<IServiceProvider, IRetryAttemptReader> reader)
    {
        _retryAttemptReader = reader;
        return this;
    }

    public override void Configure()
    {

        Services.Configure<ExpressionOptions>(options =>
        {
            options.AddTypeAlias<List<RetryAttemptRecord>>("RetryAttemptRecordList");
        });
    }

    public override void Apply()
    {
        Services.AddOptions<ResilienceOptions>();

        Services
            .AddSingleton<ResilienceStrategySerializer>()
            .AddSingleton<IActivityDescriptorModifier, ResilientActivityDescriptorModifier>()
            .AddScoped<IResilienceStrategyCatalog, ResilienceStrategyCatalog>()
            .AddScoped<IResilienceStrategyConfigEvaluator, ResilienceStrategyConfigEvaluator>()
            .AddScoped<IResilientActivityInvoker, ResilientActivityInvoker>()
            .AddScoped<IResilienceStrategySource, ConfigurationResilienceStrategySource>()
            .AddSingleton(VoidRetryAttemptRecorder.Instance)
            .AddSingleton(VoidRetryAttemptReader.Instance)
            .AddScoped<ActivityExecutionContextRetryAttemptRecorder>()
            .AddScoped<ActivityExecutionContextRetryAttemptReader>()
            .AddScoped(_retryAttemptRecorder)
            .AddScoped(_retryAttemptReader)
            .AddHandlersFrom<ResilienceFeature>();

        // Register transient exception detection infrastructure
        Services
            .AddSingleton<ITransientExceptionStrategy, DefaultTransientExceptionStrategy>()
            .AddSingleton<ITransientExceptionDetector, TransientExceptionDetector>();
    }
}