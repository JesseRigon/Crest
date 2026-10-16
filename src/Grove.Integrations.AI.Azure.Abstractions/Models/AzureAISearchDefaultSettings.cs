using System.ComponentModel.DataAnnotations;

namespace Grove.Integrations.AI.Azure.Models;

/// <summary>
/// Which connection Azure AI Search runs on. The endpoint, the authentication and the API key
/// are the connection's (an SDK connection on the Connections screen): its base URL is the
/// service endpoint, an API key credential selects key authentication, and without one the
/// connection's <c>azure:identityClientId</c> setting (or the system identity) is used.
/// </summary>
public class AzureAISearchDefaultSettings
{
    public const string GroupId = "azureAISearch";

    /// <summary>The connection used when the settings name none.</summary>
    public const string DefaultConnection = "azure-ai-search";

    [Display(Name = "Connection", Description = "The key of the connection to the search service (Connections). Its base URL is the service endpoint, e.g. https://[service].search.windows.net.", Prompt = DefaultConnection, Order = 0)]
    public string Connection { get; set; }
}
