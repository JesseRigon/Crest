using System.Reflection;
using System.Runtime.Loader;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Grove.Api;
using Grove.Environment.Shell;
using Grove.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Grove.Server.Tests;

// The API registry's conformance (docs/MVC/api.md › Hosting): every contributor Grove ships builds
// its endpoints, and every handler parameter names its binding source, so a wrong signature fails
// here and never a tenant. Contributors are discovered from the module assemblies in the test's
// output (every module Grove.Server references, plus those the test project references for this).
public sealed class ApiConformanceTests
{
    private static readonly Type[] s_frameworkParameters =
    [
        typeof(HttpContext), typeof(HttpRequest), typeof(HttpResponse), typeof(ClaimsPrincipal), typeof(CancellationToken),
        typeof(Stream), typeof(System.IO.Pipelines.PipeReader), typeof(IFormFileCollection), typeof(IFormCollection),
    ];

    [Fact]
    public async Task EveryShippedContributorBuildsItsEndpointsWithExplicitBindings()
    {
        var contributors = DiscoverContributors();
        Assert.NotEmpty(contributors);


        var services = Host();
        foreach (var contributor in contributors)
        {
            services.AddSingleton(typeof(IApiContributor), contributor);
        }

        var provider = services.BuildServiceProvider();
        var routes = new TestRouteBuilder(provider);
        await routes.MapApiRegistryAsync(provider);

        var registry = provider.GetRequiredService<ApiEndpointRegistry>();
        Assert.True(registry.Failures.Count == 0, string.Join("\n", registry.Failures.Select(failure => failure.Error)));

        var names = contributors.Select(contributor => ((IApiContributor)Activator.CreateInstance(contributor)!).Name).ToList();

        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        var violations = new List<string>();
        foreach (var endpoint in routes.DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>())
        {
            var method = endpoint.Metadata.GetMetadata<MethodInfo>();
            if (method is null)
            {
                // No handler method: a hub, or a raw RequestDelegate. A lambda whose only parameter
                // is the HttpContext binds to the RequestDelegate overload of MapGet/MapPost, which
                // discards the IResult it returns (an empty 200); ApiHandlers.Handle avoids that.
                var returned = endpoint.RequestDelegate?.Method.ReturnType;
                if (returned is { IsGenericType: true } && typeof(IResult).IsAssignableFrom(returned.GetGenericArguments()[0]))
                {
                    violations.Add($"{endpoint.DisplayName}: mapped as a raw RequestDelegate returning {returned.Name}; its result is discarded. Map it with ApiHandlers.Handle or give the lambda a bound parameter.");
                }

                continue;
            }

            foreach (var parameter in method.GetParameters())
            {
                if (!HasExplicitBinding(parameter))
                {
                    violations.Add($"{endpoint.DisplayName}: parameter '{parameter.Name}' ({parameter.ParameterType.Name}) names no binding source.");
                }
            }
        }

        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    [Fact]
    public async Task ARequestThatBreaksItsRulesIsAnsweredWithTheErrorMap()
    {
        var services = Host();
        services.AddApiContributor<ThingsApi>();
        var provider = services.BuildServiceProvider();
        var routes = new TestRouteBuilder(provider);
        await routes.MapApiRegistryAsync(provider);
        var endpoint = routes.DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().Single();

        var context = Request(provider, """{"name":"nope","url":"nope","child":{"code":""}}""");
        context.SetEndpoint(endpoint);
        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        var errors = JsonDocument.Parse(body).RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("url", out var url), body);
        Assert.Equal("url is not a valid URL.", url[0].GetString());
        Assert.True(errors.TryGetProperty("child.code", out var code), body);
        Assert.Equal("code must not be empty.", code[0].GetString());
        Assert.True(errors.TryGetProperty("", out var whole), body);
        Assert.Equal("Name and url must differ.", whole[0].GetString());
        Assert.Equal(3, errors.EnumerateObject().Count());
    }

    [Fact]
    public async Task ABodyBoundThroughTheEngineSerializerIsValidatedUnderItsOwnKeys()
    {
        var services = Host();
        var serializer = Substitute.For<Grove.Workflows.IApiSerializer>();
        serializer.GetOptions().Returns(new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        services.AddSingleton(serializer);
        services.AddApiContributor<EngineThingsApi>();
        var provider = services.BuildServiceProvider();
        var routes = new TestRouteBuilder(provider);
        await routes.MapApiRegistryAsync(provider);
        var endpoint = routes.DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().Single();

        var context = Request(provider, """{"definitionId":"d"}""");
        context.SetEndpoint(endpoint);
        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        var errors = JsonDocument.Parse(body).RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("versionOptions", out var versionOptions), body);
        Assert.Equal("VersionOptions and DefinitionId should either both be empty or both be not empty", versionOptions[0].GetString());
        Assert.Single(errors.EnumerateObject());
    }

    [Fact]
    public async Task AValidRequestReachesTheHandler()
    {
        var services = Host();
        services.AddApiContributor<ThingsApi>();
        var provider = services.BuildServiceProvider();
        var routes = new TestRouteBuilder(provider);
        await routes.MapApiRegistryAsync(provider);
        var endpoint = routes.DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().Single();

        var context = Request(provider, """{"name":"thing","url":"https://example.org","child":{"code":"c"}}""");
        context.SetEndpoint(endpoint);
        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
    }

    // A type with the static BindAsync minimal APIs look for names its own source (the workflow
    // engine's EngineBody<T>: the body through the engine's serializer).
    private static bool HasExplicitBinding(ParameterInfo parameter) =>
        s_frameworkParameters.Contains(parameter.ParameterType)
        || parameter.ParameterType.GetMethod("BindAsync", BindingFlags.Public | BindingFlags.Static, [typeof(HttpContext), typeof(ParameterInfo)]) is not null
        || parameter.GetCustomAttributes().Any(attribute => attribute is Microsoft.AspNetCore.Http.Metadata.IFromServiceMetadata
            or Microsoft.AspNetCore.Http.Metadata.IFromBodyMetadata
            or Microsoft.AspNetCore.Http.Metadata.IFromRouteMetadata
            or Microsoft.AspNetCore.Http.Metadata.IFromQueryMetadata
            or Microsoft.AspNetCore.Http.Metadata.IFromHeaderMetadata
            or Microsoft.AspNetCore.Http.Metadata.IFromFormMetadata
            or AsParametersAttribute);

    private static List<Type> DiscoverContributors()
    {
        var directory = AppContext.BaseDirectory;
        foreach (var path in Directory.EnumerateFiles(directory, "Grove.*.dll"))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == name))
            {
                continue;
            }

            try
            {
                AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
            }
            catch (BadImageFormatException)
            {
                // Native or resource files named like a module.
            }
        }

        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name is { } name && name.StartsWith("Grove.", StringComparison.Ordinal) && !name.EndsWith(".Tests", StringComparison.Ordinal))
            .SelectMany(assembly =>
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    return exception.Types.Where(type => type is not null).Select(type => type!);
                }
            })
            .Where(type => typeof(IApiContributor).IsAssignableFrom(type) && type is { IsClass: true, IsAbstract: false } && type.GetConstructor(Type.EmptyTypes) is not null)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();
    }

    private static ServiceCollection Host()
    {
        var services = new ServiceCollection();
        services.AddRouting();
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        services.AddLogging();
        services.AddSignalR();
        services.AddLocalization();
        services.AddApiValidation();
        services.AddSingleton(Substitute.For<IShellHost>());
        services.AddSingleton(Substitute.For<Microsoft.Extensions.Hosting.IHostApplicationLifetime>());
        services.AddSingleton(new ShellSettings { Name = "Conformance" });
        services.AddSingleton<DefinedApiEndpointDataSource>();
        services.AddSingleton<ApiEndpointRegistry>();
        return services;
    }

    private static DefaultHttpContext Request(IServiceProvider services, string json)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/grove/things";
        context.Request.ContentType = "application/json";
        context.Request.Headers.Authorization = "Bearer token";
        var bytes = Encoding.UTF8.GetBytes(json);
        context.Request.Body = new MemoryStream(bytes);
        context.Request.ContentLength = bytes.Length;
        context.Response.Body = new MemoryStream();
        // Minimal APIs read a body only when the server says the request can have one.
        context.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpRequestBodyDetectionFeature>(new BodyDetection());
        return context;
    }

    private sealed class BodyDetection : Microsoft.AspNetCore.Http.Features.IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }

    private sealed class ThingsApi : ApiContributor
    {
        public override string Name => "ConformanceThings";

        public override string FeatureId => "Grove.Things";

        public override void Map(IEndpointRouteBuilder group) =>
            group.MapPost("things", ([FromBody] ThingWrite write) => TypedResults.NoContent()).SkipApiAntiforgery();
    }

    private sealed class EngineThingsApi : ApiContributor
    {
        public override string Name => "ConformanceEngineThings";

        public override string FeatureId => "Grove.Things";

        public override void Map(IEndpointRouteBuilder group) =>
            group.MapPost("things", (Grove.Workflows.Api.Endpoints.EngineBody<Grove.Workflows.Api.Endpoints.WorkflowInstances.BulkCancelRequest> body) => TypedResults.NoContent()).SkipApiAntiforgery();
    }

    private sealed class ThingWrite : Validated<ThingWrite>
    {
        [NotEmpty]
        public string? Name { get; set; }

        [AbsoluteUrl]
        public string? Url { get; set; }

        public ThingChild? Child { get; set; }

        protected override void Rules(RuleSet<ThingWrite> rules) =>
            rules.Must(x => x.Name != x.Url, "Name and url must differ.");
    }

    private sealed class ThingChild
    {
        [NotEmpty]
        public string? Code { get; set; }
    }

    private sealed class TestRouteBuilder(IServiceProvider services) : IEndpointRouteBuilder
    {
        public IServiceProvider ServiceProvider => services;

        public ICollection<EndpointDataSource> DataSources { get; } = [];

        public IApplicationBuilder CreateApplicationBuilder() => new ApplicationBuilder(services);
    }
}
