using System.Net.Http.Headers;
using Crest.Workflows.Api.Client.Options;
using Microsoft.Extensions.Options;

namespace Crest.Workflows.Api.Client.HttpMessageHandlers;

/// <summary>
/// An <see cref="HttpMessageHandler"/> that configures the outgoing HTTP request to use an API key as the authorization header.
/// </summary>
public class ApiKeyHttpMessageHandler : DelegatingHandler
{
    private readonly CrestWorkflowsClientOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiKeyHttpMessageHandler"/> class.
    /// </summary>
    public ApiKeyHttpMessageHandler(IOptions<CrestWorkflowsClientOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var apiKey = _options.ApiKey;
        request.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", apiKey);

        return await base.SendAsync(request, cancellationToken);
    }
}