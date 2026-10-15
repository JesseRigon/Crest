using System.Text;
using Grove.Infrastructure.Shells.Admin.Api;
using Grove.Api;
using Grove.Environment.Shell;
using Grove.Infrastructure.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Grove.Infrastructure.Server.Tests;

// A handler mapped through ApiHandlers.Handle executes its result (a bare HttpContext-only lambda
// would bind to the RequestDelegate overload and answer an empty 200).
public sealed class ThemeApiResponseTests
{
    [Fact]
    public async Task TheThemeEndpointAnswersWithJson()
    {
        var services = new ServiceCollection();
        services.AddRouting();
        services.AddLogging();
        services.AddLocalization();
        services.AddApiValidation();
        services.AddSingleton(Substitute.For<IShellHost>());
        services.AddSingleton(new ShellSettings { Name = "Theme" });
        services.AddSingleton<DefinedApiEndpointDataSource>();
        services.AddSingleton<ApiEndpointRegistry>();
        var siteService = Substitute.For<ISiteService>();
        siteService.GetSiteSettingsAsync().Returns(new SiteSettings());
        services.AddSingleton(siteService);
        services.AddSingleton(Substitute.For<IAuthorizationService>());
        services.AddApiContributor<ThemeSettingsApi>();
        var provider = services.BuildServiceProvider();
        var routes = new TestRouteBuilder(provider);
        await routes.MapApiRegistryAsync(provider);
        var endpoint = routes.DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
            .Single(e => e.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains("GET"));

        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/grove/theme";
        context.Response.Body = new MemoryStream();
        context.SetEndpoint(endpoint);
        await endpoint.RequestDelegate!(context);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        // Bisect: the handler itself, then the endpoint.
        var direct = await ApiHandlers.For<ThemeSettingsHandlers>(context).Get();
        var directContext = new DefaultHttpContext { RequestServices = provider };
        directContext.Response.Body = new MemoryStream();
        await direct.ExecuteAsync(directContext);
        directContext.Response.Body.Position = 0;
        var directBody = await new StreamReader(directContext.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        Assert.Equal(200, context.Response.StatusCode);
        Assert.True(body.Length > 0, $"endpoint body empty; direct result {direct.GetType().Name} wrote: '{directBody}'; filters: {string.Join(",", endpoint.Metadata.Select(m => m.GetType().Name))}");
        Assert.Contains("radzenTheme", body, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TestRouteBuilder(IServiceProvider services) : IEndpointRouteBuilder
    {
        public IServiceProvider ServiceProvider => services;
        public ICollection<EndpointDataSource> DataSources { get; } = [];
        public IApplicationBuilder CreateApplicationBuilder() => new ApplicationBuilder(services);
    }
}
