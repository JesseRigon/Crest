#nullable enable

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Crest.Api;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authorization;
using Crest.Environment.Extensions.Features;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Descriptor;
using Crest.Themes;

namespace Crest.Features.Api;

public sealed class FeaturesApi : ApiContributor
{
    public override string Name => "Features";

    public override string FeatureId => "Crest.Features";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("features");
        routes.MapGet("", ApiHandlers.Handle<FeaturesHandlers>(h => h.List()));
        routes.MapPost("{id}/enable", (HttpContext __http, [FromRoute] string id) => ApiHandlers.For<FeaturesHandlers>(__http).Enable(id));
        routes.MapPost("{id}/disable", (HttpContext __http, [FromRoute] string id) => ApiHandlers.For<FeaturesHandlers>(__http).Disable(id));
    }
}

public sealed class FeaturesHandlers(
    IShellDescriptorManager shellDescriptorManager,
    IShellFeaturesManager shellFeaturesManager,
    Crest.Environment.Extensions.IExtensionManager extensionManager,
    IAuthorizationService authorizationService) : ApiHandlers
{
    public async Task<IResult> List()
    {
        if (!await authorizationService.AuthorizeAsync(User, Crest.Features.FeaturesPermissions.ManageFeatures)) return Forbid();
        var descriptor = await shellDescriptorManager.GetShellDescriptorAsync();
        var enabledIds = descriptor.Features
            .Select(feature => feature.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var featureInfos = await shellFeaturesManager.GetAvailableFeaturesAsync();
        var incompatible = (ShellCompatibility is { } compatibility ? await compatibility.GetIncompatibilitiesAsync() : [])
            .GroupBy(incompatibility => incompatibility.FeatureId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => string.Join(" ", group.Select(incompatibility => incompatibility.Reason)),
                StringComparer.OrdinalIgnoreCase);

        return Ok(featureInfos
            .Select(feature => Feature.From(feature, enabledIds.Contains(feature.Id)) with
            {
                Incompatibility = incompatible.GetValueOrDefault(feature.Id),
            })
            .OrderBy(feature => feature.Category)
            .ThenBy(feature => feature.Name)
            .ToArray());
    }

    // The shell contracts are the themes feature's; without it no theme constrains a feature.
    private IShellCompatibilityService? ShellCompatibility => HttpContext.RequestServices.GetService<IShellCompatibilityService>();

    public async Task<IResult> Enable(string id)
    {
        if (!await authorizationService.AuthorizeAsync(User, Crest.Features.FeaturesPermissions.ManageFeatures))
        {
            return Forbid();
        }

        var feature = await FindFeatureAsync(id);
        if (feature is null)
        {
            return NotFound();
        }

        // Enabling a feature whose shell contract is unsatisfiable is REFUSED, not warned
        // about: its pages would render nothing usable, and the admin has not asked for
        // that outcome - unlike a theme change, which is their own deliberate decision
        // about their site and therefore only warns (see ThemesController.SetCurrent).
        // The report names the feature, the bucket and the active theme so the refusal is
        // actionable rather than just a "no". The features this one depends on are checked
        // too: enabling it enables them, and their pages are just as unusable.
        var incompatibilities = ShellCompatibility is { } compatibility
            ? await compatibility.GetIncompatibilitiesAsync(extensionManager.GetFeatureDependencies(feature.Id).Select(dependency => dependency.Id).Append(feature.Id))
            : [];
        if (incompatibilities.Count > 0)
        {
            return Conflict(new ShellIncompatibilityReport(
                $"'{feature.Id}' needs a shell its active theme does not provide.",
                incompatibilities));
        }

        await shellFeaturesManager.EnableFeaturesAsync([feature], force: true);
        return NoContent();
    }

    public async Task<IResult> Disable(string id)
    {
        if (!await authorizationService.AuthorizeAsync(User, Crest.Features.FeaturesPermissions.ManageFeatures))
        {
            return Forbid();
        }

        var feature = await FindFeatureAsync(id);
        if (feature is null || feature.IsAlwaysEnabled)
        {
            return NotFound();
        }

        await shellFeaturesManager.DisableFeaturesAsync([feature], force: true);
        return NoContent();
    }

    private async Task<IFeatureInfo?> FindFeatureAsync(string id) => (await shellFeaturesManager.GetAvailableFeaturesAsync())
        .FirstOrDefault(feature => string.Equals(feature.Id, id, StringComparison.OrdinalIgnoreCase));
}
