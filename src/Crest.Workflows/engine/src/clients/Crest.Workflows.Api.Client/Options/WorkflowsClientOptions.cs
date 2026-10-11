namespace Crest.Workflows.Api.Client.Options;

/// <summary>
/// Represents options for the Crest.Workflows client.
/// </summary>
public class WorkflowsClientOptions
{
    /// <summary>
    /// Gets or sets the base address of the Crest.Workflows server.
    /// </summary>
    public Uri BaseAddress { get; set; } = default!;

    /// <summary>
    /// Gets or sets the API key function to use when authenticating with the Crest.Workflows server.
    /// </summary>
    public string? ApiKey { get; set; }
    
    /// <summary>
    /// Gets or sets a delegate that can be used to configure the HTTP client.
    /// </summary>
    public Action<IServiceProvider, HttpClient>? ConfigureHttpClient { get; set; }
}