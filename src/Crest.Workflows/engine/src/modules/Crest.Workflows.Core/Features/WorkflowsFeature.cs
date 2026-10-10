using Crest.Workflows.Common;
using Crest.Workflows.Common.Features;
using Crest.Workflows.Common.Serialization;
using Crest.Workflows.Expressions.Features;
using Crest.Workflows.Extensions;
using Crest.Workflows.Features.Abstractions;
using Crest.Workflows.Features.Attributes;
using Crest.Workflows.Features.Services;
using Crest.Workflows.ActivationValidators;
using Crest.Workflows.Builders;
using Crest.Workflows.CommitStates;
using Crest.Workflows.IncidentStrategies;
using Crest.Workflows.LogPersistence;
using Crest.Workflows.LogPersistence.Strategies;
using Crest.Workflows.Middleware.Activities;
using Crest.Workflows.Middleware.Workflows;
using Crest.Workflows.Pipelines.ActivityExecution;
using Crest.Workflows.Pipelines.WorkflowExecution;
using Crest.Workflows.PortResolvers;
using Crest.Workflows.Serialization.Configurators;
using Crest.Workflows.Serialization.Helpers;
using Crest.Workflows.Serialization.Serializers;
using Crest.Workflows.Services;
using Crest.Workflows.UIHints.CheckList;
using Crest.Workflows.UIHints.Dictionary;
using Crest.Workflows.UIHints.Dropdown;
using Crest.Workflows.UIHints.JsonEditor;
using Crest.Workflows.UIHints.RadioList;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Features;

/// <summary>
/// Adds workflow services to the system.
/// </summary>
[DependsOn(typeof(SystemClockFeature))]
[DependsOn(typeof(ExpressionsFeature))]
[DependsOn(typeof(MediatorFeature))]
[DependsOn(typeof(DefaultFormattersFeature))]
[DependsOn(typeof(MultitenancyFeature))]
[DependsOn(typeof(CommitStrategiesFeature))]
public class WorkflowsFeature : FeatureBase
{
    /// <inheritdoc />
    public WorkflowsFeature(IModule module) : base(module)
    {
    }

    /// <summary>
    /// A factory that instantiates a concrete <see cref="IStandardInStreamProvider"/>.
    /// </summary>
    public Func<IServiceProvider, IStandardInStreamProvider> StandardInStreamProvider { get; set; } = _ => new StandardInStreamProvider(Console.In);

    /// <summary>
    /// A factory that instantiates a concrete <see cref="IStandardOutStreamProvider"/>.
    /// </summary>
    public Func<IServiceProvider, IStandardOutStreamProvider> StandardOutStreamProvider { get; set; } = _ => new StandardOutStreamProvider(Console.Out);

    /// <summary>
    /// A factory that instantiates a concrete <see cref="IIdentityGenerator"/>.
    /// </summary>
    public Func<IServiceProvider, IIdentityGenerator> IdentityGenerator { get; set; } = sp => new RandomLongIdentityGenerator();

    /// <summary>
    /// A handler for committing workflow execution state.
    /// </summary>
    public Func<IServiceProvider, ICommitStateHandler> CommitStateHandler { get; set; } = sp => new NoopCommitStateHandler();

    /// <summary>
    /// A factory that instantiates a concrete <see cref="ILoggerStateGenerator{WorkflowExecutionContext}"/>.
    /// </summary>
    public Func<IServiceProvider, ILoggerStateGenerator<WorkflowExecutionContext>> WorkflowLoggerStateGenerator { get; set; } = sp => new WorkflowLoggerStateGenerator();

    /// <summary>
    /// A factory that instantiates a concrete <see cref="ILoggerStateGenerator{ActivityExecutionContext}"/>.
    /// </summary>
    public Func<IServiceProvider, ILoggerStateGenerator<ActivityExecutionContext>> ActivityLoggerStateGenerator { get; set; } = sp => new ActivityLoggerStateGenerator();

    /// <summary>
    /// A delegate to configure the <see cref="IWorkflowExecutionPipeline"/>.
    /// </summary>
    public Action<IWorkflowExecutionPipelineBuilder> WorkflowExecutionPipeline { get; set; } = builder => builder
        .UseExceptionHandling()
        .UseDefaultActivityScheduler();

    /// <summary>
    /// A delegate to configure the <see cref="IActivityExecutionPipeline"/>.
    /// </summary>
    public Action<IActivityExecutionPipelineBuilder> ActivityExecutionPipeline { get; set; } = builder => builder.UseDefaultActivityInvoker();

    /// <summary>
    /// Fluent method to set <see cref="StandardInStreamProvider"/>.
    /// </summary>
    public WorkflowsFeature WithStandardInStreamProvider(Func<IServiceProvider, IStandardInStreamProvider> provider)
    {
        StandardInStreamProvider = provider;
        return this;
    }

    /// <summary>
    /// Fluent method to set <see cref="StandardOutStreamProvider"/>.
    /// </summary>
    public WorkflowsFeature WithStandardOutStreamProvider(Func<IServiceProvider, IStandardOutStreamProvider> provider)
    {
        StandardOutStreamProvider = provider;
        return this;
    }

    /// <summary>
    /// Fluent method to set <see cref="IdentityGenerator"/>.
    /// </summary>
    public WorkflowsFeature WithIdentityGenerator(Func<IServiceProvider, IIdentityGenerator> generator)
    {
        IdentityGenerator = generator;
        return this;
    }

    /// <summary>
    /// Fluent method to set <see cref="ILoggerStateGenerator{WorkflowExecutionContext}"/>.
    /// </summary>
    public WorkflowsFeature WithWorkflowLoggerStateGenerator(Func<IServiceProvider, ILoggerStateGenerator<WorkflowExecutionContext>> generator)
    {
        WorkflowLoggerStateGenerator = generator;
        return this;
    }

    /// <summary>
    /// Fluent method to set <see cref="ILoggerStateGenerator{ActivityExecutionContext}"/>.
    /// </summary>
    public WorkflowsFeature WithActivityLoggerStateGenerator(Func<IServiceProvider, ILoggerStateGenerator<ActivityExecutionContext>> generator)
    {
        ActivityLoggerStateGenerator = generator;
        return this;
    }

    /// <summary>
    /// Fluent method to configure the <see cref="IWorkflowExecutionPipeline"/>.
    /// </summary>
    public WorkflowsFeature WithWorkflowExecutionPipeline(Action<IWorkflowExecutionPipelineBuilder> setup)
    {
        WorkflowExecutionPipeline = setup;
        return this;
    }

    /// <summary>
    /// Fluent method to configure the <see cref="IActivityExecutionPipeline"/>.
    /// </summary>
    public WorkflowsFeature WithActivityExecutionPipeline(Action<IActivityExecutionPipelineBuilder> setup)
    {
        ActivityExecutionPipeline = setup;
        return this;
    }

    /// <inheritdoc />
    public override void Apply()
    {
        AddCrestWorkflowsCore(Services);
    }

    private void AddCrestWorkflowsCore(IServiceCollection services)
    {
        services

            // Core.
            .AddScoped<IActivityInvoker, ActivityInvoker>()
            .AddScoped<IWorkflowRunner, WorkflowRunner>()
            .AddScoped<IActivityTestRunner, ActivityTestRunner>()
            .AddScoped<IActivityVisitor, ActivityVisitor>()
            .AddScoped<IIdentityGraphService, IdentityGraphService>()
            .AddScoped<IWorkflowGraphBuilder, WorkflowGraphBuilder>()
            .AddScoped<IWorkflowStateExtractor, WorkflowStateExtractor>()
            .AddScoped<IActivitySchedulerFactory, ActivitySchedulerFactory>()
            .AddSingleton<IWorkflowExecutionContextSchedulerStrategy, WorkflowExecutionContextSchedulerStrategy>()
            .AddSingleton<IActivityExecutionContextSchedulerStrategy, ActivityExecutionContextSchedulerStrategy>()
            .AddScoped(CommitStateHandler)
            .AddSingleton<IHasher, Hasher>()
            .AddSingleton<IStimulusHasher, StimulusHasher>()
            .AddSingleton(IdentityGenerator)
            .AddSingleton<IBookmarkPayloadSerializer>(sp => ActivatorUtilities.CreateInstance<BookmarkPayloadSerializer>(sp))
            .AddSingleton<IActivityDescriber, ActivityDescriber>()
            .AddSingleton<IActivityRegistry, ActivityRegistry>()
            .AddScoped<IActivityRegistryLookupService, ActivityRegistryLookupService>()
            .AddSingleton<IPropertyDefaultValueResolver, PropertyDefaultValueResolver>()
            .AddSingleton<IPropertyUIHandlerResolver, PropertyUIHandlerResolver>()
            .AddSingleton<IActivityFactory, ActivityFactory>()
            .AddTransient<WorkflowBuilder>()
            .AddScoped(typeof(Func<IWorkflowBuilder>), sp => () => sp.GetRequiredService<WorkflowBuilder>())
            .AddScoped<IWorkflowBuilderFactory, WorkflowBuilderFactory>()
            .AddScoped<IVariablePersistenceManager, VariablePersistenceManager>()
            .AddScoped<IIncidentStrategyResolver, DefaultIncidentStrategyResolver>()
            .AddScoped<IActivityStateFilterManager, DefaultActivityStateFilterManager>()
            .AddScoped<IWorkflowInstanceVariableReader, DefaultWorkflowInstanceVariableReader>()
            .AddScoped<IWorkflowInstanceVariableWriter, DefaultWorkflowInstanceVariableWriter>()
            .AddScoped<DefaultActivityInputEvaluator>()

            // Incident Strategies.
            .AddTransient<IIncidentStrategy, FaultStrategy>()
            .AddTransient<IIncidentStrategy, ContinueWithIncidentsStrategy>()

            // Pipelines.
            .AddScoped<IActivityExecutionPipeline>(sp => new ActivityExecutionPipeline(sp, ActivityExecutionPipeline))
            .AddScoped<IWorkflowExecutionPipeline>(sp => new WorkflowExecutionPipeline(sp, WorkflowExecutionPipeline))

            // Built-in activity services.
            .AddScoped<IActivityResolver, PropertyBasedActivityResolver>()
            .AddScoped<IActivityResolver, SwitchActivityResolver>()
            .AddSerializationOptionsConfigurator<AdditionalConvertersConfigurator>()
            .AddSerializationOptionsConfigurator<CustomConstructorConfigurator>()

            // Domain event handlers.
            .AddHandlersFrom<WorkflowsFeature>()

            // Stream providers.
            .AddScoped(StandardInStreamProvider)
            .AddScoped(StandardOutStreamProvider)

            // Storage drivers.
            .AddScoped<IStorageDriverManager, StorageDriverManager>()
            .AddStorageDriver<WorkflowStorageDriver>()
            .AddStorageDriver<WorkflowInstanceStorageDriver>()
            .AddStorageDriver<MemoryStorageDriver>()

            // Serialization.
            .AddSingleton<IWorkflowStateSerializer, JsonWorkflowStateSerializer>()
            .AddSingleton<IPayloadSerializer, JsonPayloadSerializer>()
            .AddSingleton<IActivitySerializer, JsonActivitySerializer>()
            .AddSingleton<IApiSerializer, ApiSerializer>()
            .AddSingleton<ISafeSerializer, SafeSerializer>()
            .AddSingleton<IJsonSerializer, StandardJsonSerializer>()
            .AddSingleton<SyntheticPropertiesWriter>()
            .AddSingleton<ActivityWriter>()

            // Instantiation strategies.
            .AddScoped<IWorkflowActivationStrategy, AllowAlwaysStrategy>()

            // UI.
            .AddScoped<IUIHintHandler, DropDownUIHintHandler>()
            .AddScoped<IUIHintHandler, CheckListUIHintHandler>()
            .AddScoped<IUIHintHandler, RadioListUIHintHandler>()
            .AddScoped<IUIHintHandler, JsonEditorUIHintHandler>()
            .AddScoped<IPropertyUIHandler, StaticCheckListOptionsProvider>()
            .AddScoped<IPropertyUIHandler, StaticRadioListOptionsProvider>()
            .AddScoped<IPropertyUIHandler, StaticDropDownOptionsProvider>()
            .AddScoped<IPropertyUIHandler, JsonCodeOptionsProvider>()
            .AddScoped<DictionaryValueEvaluator>()
            .AddSingleton<IActivityDescriptorModifier, DictionaryUIHintInputModifier>()

            // Logger state generators.
            .AddSingleton(WorkflowLoggerStateGenerator)
            .AddSingleton(ActivityLoggerStateGenerator)

            // Log Persistence Strategies.
            .AddScoped<ILogPersistenceStrategyService, DefaultLogPersistenceStrategyService>()
            .AddScoped<ILogPersistenceStrategy, Include>()
            .AddScoped<ILogPersistenceStrategy, Exclude>()
            .AddScoped<ILogPersistenceStrategy, Inherit>()
            .AddScoped<ILogPersistenceStrategy, Configuration>()

            // Logging
            .AddLogging();
    }
}