using Crest.Workflows.Api.Client.Options;
using Crest.Workflows.Studio.Options;

namespace Crest.Workflows.Studio.Models;

/// <summary>
/// Represents the backend api config.
/// </summary>
public class BackendApiConfig
{
    /// <summary>
    /// Gets or sets the configure http client builder action.
    /// </summary>
    public Action<CrestWorkflowsClientBuilderOptions>? ConfigureHttpClientBuilder { get; set; }
    /// <summary>
    /// Gets or sets the configure backend options action.
    /// </summary>
    public Action<BackendOptions>? ConfigureBackendOptions { get; set; }
}