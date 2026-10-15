using Grove.Workflows.Http.Options;
using Grove.Workflows.Http.Services;
using Xunit;

namespace Grove.Workflows.Tests;

// The base URL is optional configuration: the activity describer asks for the base path while the
// registries are populated on tenant activation, so an unconfigured tenant must still get one.
public sealed class HttpEndpointBasePathTests
{
    [Fact]
    public void WithoutBaseUrl_ReturnsTheRelativeBasePath()
    {
        var provider = new DefaultHttpEndpointBasePathProvider(Microsoft.Extensions.Options.Options.Create(new HttpActivityOptions()));

        Assert.Equal("/workflows/", provider.GetBasePath());
    }

    [Fact]
    public void WithoutBaseUrlOrBasePath_ReturnsTheRoot()
    {
        Assert.Equal("/", DefaultHttpEndpointBasePathProvider.GetBasePath(new HttpActivityOptions { BasePath = null }));
    }

    [Fact]
    public void WithBaseUrl_ReturnsTheAbsoluteBase()
    {
        var options = new HttpActivityOptions { BaseUrl = new Uri("https://example.test/tenant") };

        Assert.Equal("https://example.test/tenant/workflows/", DefaultHttpEndpointBasePathProvider.GetBasePath(options));
    }
}
