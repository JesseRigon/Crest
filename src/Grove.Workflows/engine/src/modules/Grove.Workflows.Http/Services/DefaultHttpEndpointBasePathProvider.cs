using Grove.Workflows.Http.Options;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;

namespace Grove.Workflows.Http.Services;

[UsedImplicitly]
public class DefaultHttpEndpointBasePathProvider(IOptions<HttpActivityOptions> options) : IHttpEndpointBasePathProvider
{
    public string GetBasePath() => GetBasePath(options.Value);

    /// <summary>
    /// The base the HTTP endpoints are reached at: absolute when a public base URL is configured
    /// (<see cref="HttpActivityOptions.BaseUrl"/>), otherwise the server-relative base path. The
    /// base URL is optional (a tenant answers on whatever host its request came in on), so its
    /// absence must not fail the callers - the activity describer asks for this while the
    /// registries are populated on tenant activation.
    /// </summary>
    public static string GetBasePath(HttpActivityOptions options)
    {
        var basePath = options.BasePath?.ToString().Trim('/');

        if (options.BaseUrl is null)
        {
            return string.IsNullOrEmpty(basePath) ? "/" : "/" + basePath + "/";
        }

        var baseUrl = new Uri(options.BaseUrl.ToString().TrimEnd('/') + '/');
        var completeBaseUrl = new Uri(baseUrl, basePath);

        return completeBaseUrl.ToString().TrimEnd('/') + '/';
    }
}
