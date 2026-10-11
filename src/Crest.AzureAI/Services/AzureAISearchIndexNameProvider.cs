using Microsoft.Extensions.Options;
using Crest.Environment.Shell;
using Crest.Indexing;
using Crest.AzureAI.Models;

namespace Crest.AzureAI.Services;

public sealed class AzureAISearchIndexNameProvider : IIndexNameProvider
{
    private readonly ShellSettings _shellSettings;
    private readonly IOptionsMonitor<AzureAISearchDefaultOptions> _azureAIOptions;

    public AzureAISearchIndexNameProvider(
        ShellSettings shellSettings,
        IOptionsMonitor<AzureAISearchDefaultOptions> azureAIOptions)
    {
        _shellSettings = shellSettings;
        _azureAIOptions = azureAIOptions;
    }

    public string GetFullIndexName(string indexName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(indexName);

        return GetIndexPrefix() + '-' + indexName;
    }

    private string GetIndexPrefix()
    {
        var prefix = _shellSettings.Name.ToLowerInvariant();
        var azureAIOptions = _azureAIOptions.CurrentValue;

        if (!string.IsNullOrWhiteSpace(azureAIOptions.IndexesPrefix))
        {
            prefix = $"{azureAIOptions.IndexesPrefix.ToLowerInvariant()}-{prefix}";
        }

        if (AzureAISearchIndexNamingHelper.TryGetSafePrefix(prefix, out var safePrefix))
        {
            return safePrefix;
        }

        throw new InvalidOperationException($"Unable to create a safe index prefix for AI Search. Attempted to created a safe name using '{safePrefix}'.");
    }
}
