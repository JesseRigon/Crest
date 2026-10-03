using Crest.Workflows.Api.Client.Options;
using Crest.Workflows.Api.Client.Resources.ActivityDescriptorOptions.Contracts;
using Crest.Workflows.Api.Client.Resources.ActivityDescriptors.Contracts;
using Crest.Workflows.Api.Client.Resources.ActivityExecutions.Contracts;
using Crest.Workflows.Api.Client.Resources.Alterations.Contracts;
using Crest.Workflows.Api.Client.Resources.CommitStrategies.Contracts;
using Crest.Workflows.Api.Client.Resources.Features.Contracts;
using Crest.Workflows.Api.Client.Resources.Identity.Contracts;
using Crest.Workflows.Api.Client.Resources.IncidentStrategies.Contracts;
using Crest.Workflows.Api.Client.Resources.LogPersistenceStrategies;
using Crest.Workflows.Api.Client.Resources.Resilience.Contracts;
using Crest.Workflows.Api.Client.Resources.Scripting.Contracts;
using Crest.Workflows.Api.Client.Resources.StorageDrivers.Contracts;
using Crest.Workflows.Api.Client.Resources.Tasks.Contracts;
using Crest.Workflows.Api.Client.Resources.Tests;
using Crest.Workflows.Api.Client.Resources.VariableTypes.Contracts;
using Crest.Workflows.Api.Client.Resources.WorkflowActivationStrategies.Contracts;
using Crest.Workflows.Api.Client.Resources.WorkflowDefinitions.Contracts;
using Crest.Workflows.Api.Client.Resources.WorkflowExecutionContexts.Contracts;
using Crest.Workflows.Api.Client.Resources.WorkflowInstances.Contracts;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Refit;
using static Crest.Workflows.Api.Client.RefitSettingsHelper;

namespace Crest.Workflows.Api.Client.Extensions;

/// <summary>
/// Provides extension methods for dependency injection.
/// </summary>
[PublicAPI]
public static class DependencyInjectionExtensions
{
    /// <summary>
    /// Adds default Crest.Workflows API clients configured to use an API key.
    /// </summary>
    public static IServiceCollection AddDefaultApiClientsUsingApiKey(this IServiceCollection services, Action<CrestWorkflowsClientOptions> configureOptions)
    {
        var options = new CrestWorkflowsClientOptions();
        configureOptions(options);

        return services.AddDefaultApiClients(client =>
        {
            client.BaseAddress = options.BaseAddress;
            client.ApiKey = options.ApiKey;
            client.ConfigureHttpClient = options.ConfigureHttpClient;
        });
    }

    /// <summary>
    /// Adds default Crest.Workflows API clients.
    /// </summary>
    public static IServiceCollection AddDefaultApiClients(this IServiceCollection services, Action<CrestWorkflowsClientBuilderOptions>? configureClient = null)
    {
        return services.AddApiClients(configureClient, builderOptions =>
        {
            var builderOptionsWithoutRetryPolicy = new CrestWorkflowsClientBuilderOptions
            {
                ApiKey = builderOptions.ApiKey,
                AuthenticationHandler = builderOptions.AuthenticationHandler,
                BaseAddress = builderOptions.BaseAddress,
                ConfigureHttpClient = builderOptions.ConfigureHttpClient,
                ConfigureHttpClientBuilder = builderOptions.ConfigureHttpClientBuilder,
                ConfigureRetryPolicy = null
            };

            services.AddApi<IWorkflowDefinitionsApi>(builderOptions);
            services.AddApi<IExecuteWorkflowApi>(builderOptionsWithoutRetryPolicy);
            services.AddApi<IWorkflowInstancesApi>(builderOptions);
            services.AddApi<IActivityDescriptorsApi>(builderOptions);
            services.AddApi<IActivityDescriptorOptionsApi>(builderOptions);
            services.AddApi<IActivityExecutionsApi>(builderOptions);
            services.AddApi<IStorageDriversApi>(builderOptions);
            services.AddApi<IVariableTypesApi>(builderOptions);
            services.AddApi<IWorkflowActivationStrategiesApi>(builderOptions);
            services.AddApi<IIncidentStrategiesApi>(builderOptions);
            services.AddApi<ILogPersistenceStrategiesApi>(builderOptions);
            services.AddApi<ICommitStrategiesApi>(builderOptions);
            services.AddApi<IResilienceStrategiesApi>(builderOptions);
            services.AddApi<IRetryAttemptsApi>(builderOptions);
            services.AddApi<ILoginApi>(builderOptions);
            services.AddApi<IFeaturesApi>(builderOptions);
            services.AddApi<IJavaScriptApi>(builderOptions);
            services.AddApi<IExpressionDescriptorsApi>(builderOptions);
            services.AddApi<IWorkflowContextProviderDescriptorsApi>(builderOptions);
            services.AddApi<IAlterationsApi>(builderOptions);
            services.AddApi<ITasksApi>(builderOptions);
            services.AddApi<ITestsApi>(builderOptions);
        });
    }

    /// <summary>
    /// Adds an API client to the service collection. Requires AddCrestWorkflowsClient to be called exactly once.
    /// </summary>
    public static IServiceCollection AddApiClient<T>(this IServiceCollection services, Action<CrestWorkflowsClientBuilderOptions>? configureClient = null) where T : class
    {
        return services.AddApiClients(configureClient, builderOptions => services.AddApi<T>(builderOptions));
    }

    /// <summary>
    /// Adds the Crest.Workflows client to the service collection.
    /// </summary>
    public static IServiceCollection AddApiClients(this IServiceCollection services, Action<CrestWorkflowsClientBuilderOptions>? configureClient = null, Action<CrestWorkflowsClientBuilderOptions>? configureServices = null)
    {
        var builderOptions = new CrestWorkflowsClientBuilderOptions();
        configureClient?.Invoke(builderOptions);
        builderOptions.ConfigureHttpClientBuilder += builder => builder.AddHttpMessageHandler(sp => (DelegatingHandler)sp.GetRequiredService(builderOptions.AuthenticationHandler));

        services.TryAddScoped(builderOptions.AuthenticationHandler);

        services.Configure<CrestWorkflowsClientOptions>(options =>
        {
            options.BaseAddress = builderOptions.BaseAddress;
            options.ConfigureHttpClient = builderOptions.ConfigureHttpClient;
            options.ApiKey = builderOptions.ApiKey;
        });

        configureServices?.Invoke(builderOptions);
        return services;
    }

    /// <summary>
    /// Adds a refit client for the specified API type.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="httpClientBuilderOptions">An options object that can be used to configure the HTTP client builder.</param>
    /// <typeparam name="T">The type representing the API.</typeparam>
    public static IServiceCollection AddApi<T>(this IServiceCollection services, CrestWorkflowsClientBuilderOptions? httpClientBuilderOptions = default) where T : class
    {
        return services.AddApi(typeof(T), httpClientBuilderOptions);
    }

    /// <summary>
    /// Adds a refit client for the specified API type.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="apiType">The type representing the API</param>
    /// <param name="httpClientBuilderOptions">An options object that can be used to configure the HTTP client builder.</param>
    public static IServiceCollection AddApi(this IServiceCollection services, Type apiType, CrestWorkflowsClientBuilderOptions? httpClientBuilderOptions = default)
    {
        var builder = services.AddRefitClient(apiType, sp => CreateRefitSettings(sp, httpClientBuilderOptions?.ConfigureJsonSerializerOptions), apiType.Name).ConfigureHttpClient(ConfigureCrestWorkflowsApiHttpClient);
        httpClientBuilderOptions?.ConfigureHttpClientBuilder(builder);
        httpClientBuilderOptions?.ConfigureRetryPolicy?.Invoke(builder);
        return services;
    }

    /// <summary>
    /// Adds a refit client for the specified API type.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="httpClientBuilderOptions">An options object that can be used to configure the HTTP client builder.</param>
    /// <typeparam name="T">The type representing the API.</typeparam>
    public static void AddApiWithoutRetryPolicy<T>(this IServiceCollection services, CrestWorkflowsClientBuilderOptions? httpClientBuilderOptions = default) where T : class
    {
        var builder = services
            .AddRefitClient<T>(sp => CreateRefitSettings(sp), typeof(T).Name)
            .ConfigureHttpClient(ConfigureCrestWorkflowsApiHttpClient);
        httpClientBuilderOptions?.ConfigureHttpClientBuilder(builder);
    }

    /// <summary>
    /// Creates an API client for the specified API type.
    /// </summary>
    public static T CreateApi<T>(this IServiceProvider serviceProvider, Uri baseAddress) where T : class
    {
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient(typeof(T).Name);
        httpClient.BaseAddress = baseAddress;
        return CreateApi<T>(serviceProvider, httpClient);
    }

    /// <summary>
    /// Creates an API client for the specified API type.
    /// </summary>
    public static T CreateApi<T>(this IServiceProvider serviceProvider, HttpClient httpClient) where T : class
    {
        return RestService.For<T>(httpClient, CreateRefitSettings(serviceProvider));
    }

    private static void ConfigureCrestWorkflowsApiHttpClient(IServiceProvider serviceProvider, HttpClient httpClient)
    {
        var options = serviceProvider.GetRequiredService<IOptions<CrestWorkflowsClientOptions>>().Value;
        httpClient.BaseAddress = options.BaseAddress;
        options.ConfigureHttpClient?.Invoke(serviceProvider, httpClient);
    }
}