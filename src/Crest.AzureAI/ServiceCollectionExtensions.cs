using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Crest.AzureAI.Services;
using Crest.Indexing;

namespace Crest.AzureAI;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddAzureAISearchIndexingSource(
        this IServiceCollection services,
        string implementationType,
        Action<IndexingOptionsEntry> action = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(implementationType);

        services.AddIndexingSource<AzureAISearchIndexManager, AzureAISearchDocumentIndexManager, AzureAISearchIndexNameProvider>(
            AzureAISearchConstants.ProviderName, implementationType, action);

        services
            .AddOptions<IndexingOptions>()
            .Configure<IStringLocalizer<AzureAISearchLocalizationMarker>>((options, S) =>
                options.AddIndexingProvider(AzureAISearchConstants.ProviderName, provider => provider.DisplayName = S["Azure AI Search"]));

        return services;
    }

    private sealed class AzureAISearchLocalizationMarker
    {
    }
}
