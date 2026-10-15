using System.Reflection;
using Grove.Workflows.Features.Contracts;
using Grove.Workflows.Features.Models;
using Grove.Workflows.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Workflows.Api.Endpoints.Platform;

/// <summary>The engine's own facts: installed features and the package version.</summary>
public static class PlatformEndpoints
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/features/installed", ListFeatures).RequireAuthorization(EngineApiPolicies.ReadInstalledFeatures);
        routes.MapGet("/features/installed/{fullName}", GetFeature).RequireAuthorization(EngineApiPolicies.ReadInstalledFeatures);
        routes.MapGet("/package/version", GetPackageVersion).AllowAnonymous();
    }

    private static IResult ListFeatures(HttpContext context, [FromServices] IInstalledFeatureRegistry installedFeatureRegistry) =>
        EngineResults.Json(context, new ListResponse<FeatureDescriptor>(installedFeatureRegistry.List().ToList()));

    private static IResult GetFeature(HttpContext context, [FromRoute] string fullName, [FromServices] IInstalledFeatureRegistry installedFeatureRegistry)
    {
        var descriptor = installedFeatureRegistry.Find(fullName);
        return descriptor == null ? EngineResults.NotFound() : EngineResults.Json(context, descriptor);
    }

    private static IResult GetPackageVersion(HttpContext context)
    {
        var versionAttribute = typeof(PlatformEndpoints).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        var version = versionAttribute?.InformationalVersion.Split("+")[0]!;
        return EngineResults.Json(context, new PackageVersionResponse(version));
    }
}

public record PackageVersionResponse(string PackageVersion);
