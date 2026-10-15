using Grove.Api;
using Grove.Environment.Shell;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Grove.Infrastructure.Server.Tests;

// The API endpoint registry (docs/MVC/api.md › The seam, P15): the defined layer is one data source
// per tenant, built only from that tenant's sources; the declared layer maps each contributor into
// its route group, a later registration of a name replacing an earlier one.
public sealed class ApiRegistryTests
{
    [Fact]
    public async Task EachTenantServesOnlyItsOwnDefinedEntries()
    {
        var (tenantA, servicesA) = Tenant("TenantA", new FixedSource("workflows/orders/{id}"));
        var (tenantB, servicesB) = Tenant("TenantB", new FixedSource("workflows/invoices"));

        await tenantA.ReloadAsync(servicesA, TestContext.Current.CancellationToken);
        await tenantB.ReloadAsync(servicesB, TestContext.Current.CancellationToken);

        Assert.Equal(["/workflows/orders/{id}"], Patterns(tenantA));
        Assert.Equal(["/workflows/invoices"], Patterns(tenantB));
    }

    [Fact]
    public async Task ReloadingOneTenantLeavesTheOtherAlone()
    {
        var sourceA = new FixedSource("workflows/orders");
        var (tenantA, servicesA) = Tenant("TenantA", sourceA);
        var (tenantB, servicesB) = Tenant("TenantB", new FixedSource("workflows/invoices"));
        await tenantA.ReloadAsync(servicesA, TestContext.Current.CancellationToken);
        await tenantB.ReloadAsync(servicesB, TestContext.Current.CancellationToken);
        var tokenB = tenantB.GetChangeToken();
        var tokenA = tenantA.GetChangeToken();

        sourceA.Routes = ["workflows/orders", "workflows/returns"];
        await tenantA.ReloadAsync(servicesA, TestContext.Current.CancellationToken);

        Assert.True(tokenA.HasChanged);
        Assert.False(tokenB.HasChanged);
        Assert.Equal(["/workflows/orders", "/workflows/returns"], Patterns(tenantA));
        Assert.Equal(["/workflows/invoices"], Patterns(tenantB));
    }

    [Fact]
    public async Task AReloadWithTheSameRoutesKeepsTheEndpoints()
    {
        var (tenant, services) = Tenant("TenantA", new FixedSource("workflows/orders"));
        await tenant.ReloadAsync(services, TestContext.Current.CancellationToken);
        var token = tenant.GetChangeToken();

        await tenant.ReloadAsync(services, TestContext.Current.CancellationToken);

        Assert.False(token.HasChanged);
    }

    [Fact]
    public async Task AFailingSourceDoesNotTakeTheOthersDown()
    {
        var (tenant, services) = Tenant("TenantA", new FixedSource("workflows/orders"), new FailingSource());

        await tenant.ReloadAsync(services, TestContext.Current.CancellationToken);

        Assert.Equal(["/workflows/orders"], Patterns(tenant));
    }

    [Fact]
    public async Task ALaterContributorOfTheSameNameReplacesTheEarlierOne()
    {
        var services = new ServiceCollection()
            .AddRouting()
            .AddLogging()
            .AddSingleton(Substitute.For<IShellHost>())
            .AddSingleton(new ShellSettings { Name = "TenantA" })
            .AddSingleton<DefinedApiEndpointDataSource>()
            .AddSingleton<ApiEndpointRegistry>();
        services.AddApiContributor<OriginalThings>();
        services.AddApiContributor<OverridingThings>();
        services.AddApiContributor<ProtocolThings>();
        var provider = services.BuildServiceProvider();
        var routes = new TestRouteBuilder(provider);

        await routes.MapApiRegistryAsync(provider);

        var declared = provider.GetRequiredService<ApiEndpointRegistry>().Declared;
        var things = Assert.Single(declared, registration => registration.Name == "Things");
        Assert.Equal("Grove.Overrides", things.FeatureId);
        Assert.Equal(["api/grove/things/override"], things.Routes.Select(route => route.Pattern.TrimStart('/')));
        var protocol = Assert.Single(declared, registration => registration.Name == "Protocol");
        Assert.Equal(["hooks/ping"], protocol.Routes.Select(route => route.Pattern.TrimStart('/')));
    }

    [Fact]
    public async Task AContributorWhoseEndpointsCannotBeBuiltIsLeftOutAndReported()
    {
        var services = new ServiceCollection()
            .AddRouting()
            .AddLogging()
            .AddSingleton(Substitute.For<IShellHost>())
            .AddSingleton(new ShellSettings { Name = "TenantA" })
            .AddSingleton<DefinedApiEndpointDataSource>()
            .AddSingleton<ApiEndpointRegistry>()
            .AddScoped<ParsingService>();
        services.AddApiContributor<OriginalThings>();
        services.AddApiContributor<UnbindableThings>();
        var provider = services.BuildServiceProvider();
        var routes = new TestRouteBuilder(provider);

        await routes.MapApiRegistryAsync(provider);

        var registry = provider.GetRequiredService<ApiEndpointRegistry>();
        var failure = Assert.Single(registry.Failures);
        Assert.Equal("Unbindable", failure.Name);
        Assert.Contains("ParsingService", failure.Error, StringComparison.Ordinal);
        Assert.Contains("[FromServices]", failure.Error, StringComparison.Ordinal);
        Assert.IsType<ApiContributorException>(failure.Exception);
        Assert.Equal(["Things"], registry.Declared.Select(registration => registration.Name));
        Assert.Contains("api/grove/things", routes.DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().Select(endpoint => endpoint.RoutePattern.RawText!.TrimStart('/')));
    }

    [Theory]
    [InlineData("GET", false)]
    [InlineData("HEAD", false)]
    [InlineData("POST", true)]
    [InlineData("PUT", true)]
    [InlineData("DELETE", true)]
    public void StateChangingCallsNeedTheAntiforgeryToken(string method, bool validated)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;

        Assert.Equal(validated, ApiAntiforgeryEndpointFilter.RequiresValidation(context));
    }

    [Fact]
    public void AnEndpointCanSkipTheAntiforgeryToken()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(SkipApiAntiforgeryMetadata.Instance), "callback"));

        Assert.False(ApiAntiforgeryEndpointFilter.RequiresValidation(context));
    }

    private static (DefinedApiEndpointDataSource DataSource, IServiceProvider Services) Tenant(string name, params IDefinedApiSource[] sources)
    {
        var services = new ServiceCollection();
        foreach (var source in sources)
        {
            services.AddSingleton(source);
        }

        var dataSource = new DefinedApiEndpointDataSource(Substitute.For<IShellHost>(), new ShellSettings { Name = name }, NullLogger<DefinedApiEndpointDataSource>.Instance);
        return (dataSource, services.BuildServiceProvider());
    }

    private static string[] Patterns(EndpointDataSource dataSource) =>
        dataSource.Endpoints.OfType<RouteEndpoint>().Select(endpoint => endpoint.RoutePattern.RawText!).Order(StringComparer.Ordinal).ToArray();

    private sealed class FixedSource(params string[] routes) : IDefinedApiSource
    {
        public string[] Routes { get; set; } = routes;

        public string Name => "fixed";

        public Task<IReadOnlyList<DefinedApiEntry>> GetEntriesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DefinedApiEntry>>(Routes
                .Select(route => new DefinedApiEntry(route, Name, "/" + route, [], _ => Task.CompletedTask))
                .ToArray());
    }

    private sealed class FailingSource : IDefinedApiSource
    {
        public string Name => "failing";

        public Task<IReadOnlyList<DefinedApiEntry>> GetEntriesAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The store is not there.");
    }

    private sealed class OriginalThings : ApiContributor
    {
        public override string Name => "Things";

        public override string FeatureId => "Grove.Things";

        public override void Map(IEndpointRouteBuilder group) => group.MapGet("things", () => "original");
    }

    private sealed class OverridingThings : ApiContributor
    {
        public override string Name => "Things";

        public override string FeatureId => "Grove.Overrides";

        public override void Map(IEndpointRouteBuilder group) => group.MapGet("things/override", () => "override");
    }

    private sealed class ProtocolThings : ApiContributor
    {
        public override string Name => "Protocol";

        public override string FeatureId => "Grove.Things";

        public override string RoutePrefix => "hooks";

        public override void Map(IEndpointRouteBuilder group) => group.MapPost("ping", () => "pong");
    }

    // A service with an instance TryParse: minimal APIs try to bind it from the request and throw.
    private sealed class ParsingService
    {
        public bool TryParse(string source, out object? value, out string? error)
        {
            value = source;
            error = null;
            return true;
        }
    }

    private sealed class UnbindableThings : ApiContributor
    {
        public override string Name => "Unbindable";

        public override string FeatureId => "Grove.Unbindable";

        public override void Map(IEndpointRouteBuilder group) => group.MapGet("unbindable", (ParsingService parsing) => "never");
    }

    private sealed class TestRouteBuilder(IServiceProvider services) : IEndpointRouteBuilder
    {
        public IServiceProvider ServiceProvider => services;

        public ICollection<EndpointDataSource> DataSources { get; } = [];

        public IApplicationBuilder CreateApplicationBuilder() => new ApplicationBuilder(services);
    }
}
