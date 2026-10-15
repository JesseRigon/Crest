using Grove.Workflows.Api.Client.Options;
using Grove.Workflows.Studio.Options;

namespace Grove.Workflows.Studio.Models;

/// <summary>
/// Represents the backend api config.
/// </summary>
public class BackendApiConfig
{
    /// <summary>
    /// Gets or sets the configure http client builder action.
    /// </summary>
    public Action<WorkflowsClientBuilderOptions>? ConfigureHttpClientBuilder { get; set; }
    /// <summary>
    /// Gets or sets the configure backend options action.
    /// </summary>
    public Action<BackendOptions>? ConfigureBackendOptions { get; set; }
}