using Grove.Environment.Shell;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grove.Api;

/// <summary>
/// Maps the tenant's registry into its route builder (docs/MVC/api.md › The seam): each declared
/// contributor into a route group with the hosting plumbing, and the defined layer as a dynamic
/// data source.
/// </summary>
/// <remarks>
/// ASP.NET builds a tenant's endpoints lazily and all at once, on the tenant's first request: a
/// handler the request delegate factory rejects (a parameter it cannot bind, a bad route) would
/// fail every request of the tenant with an exception that names neither the module nor the
/// handler. So each contributor's endpoints are built here, right after it maps them. One that
/// throws is left out, the others keep serving, and the failure is logged as an error with the
/// contributor, its feature and the full stack trace, kept in the registry and reported by the
/// <c>api-registry</c> health check.
/// </remarks>
internal static class ApiEndpointMapper
{
    public static async Task MapApiRegistryAsync(this IEndpointRouteBuilder routes, IServiceProvider services)
    {
        var registry = services.GetRequiredService<ApiEndpointRegistry>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ApiEndpointMapper).FullName!);
        var tenant = services.GetService<ShellSettings>()?.Name ?? string.Empty;
        var declared = new List<IApiContributor>();
        var failures = new List<ApiRegistrationFailure>();

        // Last registration of a name wins: a downstream module overrides a module's API by
        // registering a contributor under the same name.
        var contributors = services.GetServices<IApiContributor>()
            .GroupBy(contributor => contributor.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last());

        foreach (var contributor in contributors)
        {
            var existing = routes.DataSources.ToList();
            try
            {
                var group = routes.MapGroup(contributor.RoutePrefix);
                group.AddEndpointFilter<ApiExceptionEndpointFilter>();
                group.AddEndpointFilter<ApiAntiforgeryEndpointFilter>();
                group.AddEndpointFilter<ApiValidationSinkEndpointFilter>();
                group.WithTags(contributor.Name);
                group.WithMetadata(new ApiContributorMetadata(contributor.Name, contributor.FeatureId));

                contributor.Map(group);

                foreach (var source in routes.DataSources.Except(existing))
                {
                    _ = source.Endpoints;
                }

                declared.Add(contributor);
            }
            catch (Exception exception)
            {
                foreach (var source in routes.DataSources.Except(existing).ToList())
                {
                    routes.DataSources.Remove(source);
                }

                var failure = new ApiContributorException(contributor, exception);
                failures.Add(new ApiRegistrationFailure(contributor.Name, contributor.FeatureId, contributor.GetType().FullName ?? contributor.Name, failure.Message, failure));
                logger.LogError(failure,
                    "Tenant '{Tenant}': the API contributor '{Contributor}' ({ContributorType}, feature '{FeatureId}') was left out: {Error}",
                    tenant, contributor.Name, contributor.GetType().FullName, contributor.FeatureId, failure.Message);
            }
        }

        registry.SetDeclared(declared, failures, routes);

        var defined = services.GetRequiredService<DefinedApiEndpointDataSource>();
        await defined.ReloadAsync(services);
        routes.DataSources.Add(defined);
    }
}

/// <summary>
/// Building a contributor's endpoints failed. The message names the contributor, its type and
/// feature, the cause, and the usual fix; the inner exception carries the original stack trace.
/// </summary>
public sealed class ApiContributorException(IApiContributor contributor, Exception innerException)
    : Exception(Describe(contributor, innerException), innerException)
{
    public string Contributor { get; } = contributor.Name;

    public string FeatureId { get; } = contributor.FeatureId;

    private static string Describe(IApiContributor contributor, Exception exception)
    {
        var cause = exception.GetBaseException();
        var message = $"API contributor '{contributor.Name}' ({contributor.GetType().FullName}, feature '{contributor.FeatureId}', prefix '{contributor.RoutePrefix}') could not build its endpoints: {cause.GetType().Name}: {cause.Message}";

        // Minimal APIs bind a handler parameter from the request when its type has a TryParse or
        // BindAsync member, and throw when that member does not have the expected shape - even
        // for a type that is meant to be injected as a service.
        if (cause.Message.Contains("TryParse", StringComparison.Ordinal) || cause.Message.Contains("BindAsync", StringComparison.Ordinal))
        {
            message += " Fix: rename the member on that type, or mark the handler parameter [FromServices].";
        }

        return message;
    }
}

/// <summary>Endpoint metadata naming the contributor that declared the endpoint.</summary>
public sealed record ApiContributorMetadata(string Name, string FeatureId);
