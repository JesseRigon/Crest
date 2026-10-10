using Crest.Workflows.Http.Options;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;

namespace Crest.Workflows.Http.Services;

[UsedImplicitly]
public class DefaultHttpEndpointBasePathProvider(IOptions<HttpActivityOptions> options) : IHttpEndpointBasePathProvider
{
    public string GetBasePath()
    {
        var baseUrl = new Uri(options.Value.BaseUrl.ToString().TrimEnd('/') + '/');
        var basePath = options.Value.BasePath?.ToString().Trim('/');
        var completeBaseUrl = new Uri(baseUrl, basePath);

        return completeBaseUrl.ToString().TrimEnd('/') + '/';
    }
}