using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Crest.Environment.Options;
using Crest.AzureAI.Handlers;
using Crest.AzureAI.Models;
using Crest.AzureAI.Services;

namespace Crest.AzureAI;

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
