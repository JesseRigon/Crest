using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Descriptor;
using Crest.ViewModels;
using Crest.Themes;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/features")]
public sealed class FeaturesController(
    IShellDescriptorManager shellDescriptorManager,
    IShellFeaturesManager shellFeaturesManager,
    IShellCompatibilityService shellCompatibility,
    IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Feature[]>> List()
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.Features.FeaturesPermissions.ManageFeatures)) return Forbid();
        var descriptor = await shellDescriptorManager.GetShellDescriptorAsync();
        var enabledIds = descriptor.Features
            .Select(feature => feature.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var featureInfos = await shellFeaturesManager.GetAvailableFeaturesAsync();

        return Ok(featureInfos
            .Select(feature => Feature.From(feature, enabledIds.Contains(feature.Id)))
            .OrderBy(feature => feature.Category)
            .ThenBy(feature => feature.Name)
            .ToArray());
    }

    [HttpPost("{id}/enable")]
    public async Task<IActionResult> Enable(string id)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.Features.FeaturesPermissions.ManageFeatures))
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
        // actionable rather than just a "no".
        var incompatibilities = await shellCompatibility.GetIncompatibilitiesAsync([feature.Id]);
        if (incompatibilities.Count > 0)
        {
            return Conflict(new ShellIncompatibilityReport(
                $"'{feature.Id}' needs a shell its active theme does not provide.",
                incompatibilities));
        }

        await shellFeaturesManager.EnableFeaturesAsync([feature], force: true);
        return NoContent();
    }

    [HttpPost("{id}/disable")]
    public async Task<IActionResult> Disable(string id)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.Features.FeaturesPermissions.ManageFeatures))
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
