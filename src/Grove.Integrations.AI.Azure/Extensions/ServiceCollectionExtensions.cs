using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Environment.Options;
using Grove.Integrations.AI.Azure.Handlers;
using Grove.Integrations.AI.Azure.Models;
using Grove.Integrations.AI.Azure.Services;

namespace Grove.Integrations.AI.Azure;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddAzureAISearchServices(this IServiceCollection services)
    {
        services.AddAzureClientsCore();
        services.AddSignalOptionsChangeTokenSource<AzureAISearchDefaultOptions>();
        services.AddTransient<IConfigureOptions<AzureAISearchDefaultOptions>, AzureAISearchDefaultOptionsConfigurations>();
        services.AddScoped<AzureAISearchContentFieldMapper>();
        services.AddScoped<IAzureAISearchFieldIndexEvents, DefaultAzureAISearchFieldIndexEvents>();
        services.AddSingleton<AzureAIClientFactory>();

        return services;
    }
}
