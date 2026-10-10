using Crest.Workflows.Caching.Features;
using Crest.Workflows.Common.Features;
using Crest.Workflows.Expressions.Features;
using Crest.Workflows.Extensions;
using Crest.Workflows.Features.Abstractions;
using Crest.Workflows.Features.Attributes;
using Crest.Workflows.Features.Services;
using Crest.Workflows.Expressions.JavaScript.Activities;
using Crest.Workflows.Expressions.JavaScript.Contracts;
using Crest.Workflows.Expressions.JavaScript.Extensions;
using Crest.Workflows.Expressions.JavaScript.HostedServices;
using Crest.Workflows.Expressions.JavaScript.Options;
using Crest.Workflows.Expressions.JavaScript.Providers;
using Crest.Workflows.Expressions.JavaScript.Services;
using Crest.Workflows.Expressions.JavaScript.TypeDefinitions.Contracts;
using Crest.Workflows.Expressions.JavaScript.TypeDefinitions.Services;
using Crest.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Expressions.JavaScript.Features;

/// <summary>
/// Installs JavaScript integration.
/// </summary>
[DependsOn(typeof(MediatorFeature))]
[DependsOn(typeof(ExpressionsFeature))]
[DependsOn(typeof(MemoryCacheFeature))]
public class JavaScriptFeature : FeatureBase
{
    /// <inheritdoc />
    public JavaScriptFeature(IModule module) : base(module)
    {
    }

    /// <summary>
    /// Configures the Jint options.
    /// </summary>
    private Action<JintOptions> JintOptions { get; set; } = _ => { };

    public JavaScriptFeature ConfigureJintOptions(Action<JintOptions> configure)
    {
        JintOptions += configure;
        return this;
    }

    /// <inheritdoc />
    public override void ConfigureHostedServices()
    {
        ConfigureHostedService<RegisterVariableTypesWithJavaScriptHostedService>();
    }

    /// <inheritdoc />
    public override void Configure()
    {
        Module.AddFastEndpointsAssembly<JavaScriptFeature>();
    }

    /// <inheritdoc />
    public override void Apply()
    {
        Services.Configure(JintOptions);

        // JavaScript services.
        Services
            .AddScoped<IJavaScriptEvaluator, JintJavaScriptEvaluator>()
            .AddScoped<ITypeDefinitionService, TypeDefinitionService>()
            .AddExpressionDescriptorProvider<JavaScriptExpressionDescriptorProvider>()
            ;

        // Type definition services.
        Services
            .AddScoped<ITypeDefinitionService, TypeDefinitionService>()
            .AddScoped<ITypeDescriber, TypeDescriber>()
            .AddScoped<ITypeDefinitionDocumentRenderer, TypeDefinitionDocumentRenderer>()
            .AddSingleton<ITypeAliasRegistry, TypeAliasRegistry>()
            .AddFunctionDefinitionProvider<CommonFunctionsDefinitionProvider>()
            .AddFunctionDefinitionProvider<ActivityOutputFunctionsDefinitionProvider>()
            .AddFunctionDefinitionProvider<RunJavaScriptFunctionsDefinitionProvider>()
            .AddTypeDefinitionProvider<CommonTypeDefinitionProvider>()
            .AddTypeDefinitionProvider<VariableTypeDefinitionProvider>()
            .AddTypeDefinitionProvider<WorkflowVariablesTypeDefinitionProvider>()
            .AddVariableDefinitionProvider<WorkflowVariablesVariableProvider>()
            ;

        // Handlers.
        Services.AddNotificationHandlersFrom<JavaScriptFeature>();

        // Activities.
        Module.UseWorkflowManagement(management => management.AddActivity<RunJavaScript>());

        // Type Script definitions.
        Services.AddFunctionDefinitionProvider<InputFunctionsDefinitionProvider>();

        // UI property handlers.
        Services.AddScoped<IPropertyUIHandler, RunJavaScriptOptionsProvider>();
    }
}