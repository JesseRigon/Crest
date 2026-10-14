using Crest.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Removing;
using Crest.Tenants;
using TenantsPermissions = Crest.Tenants.Permissions;
using Crest.ViewModels;
// The wire model is the admin client's own (one CrestTenant, not a hand-kept server copy).
using CrestTenant = Crest.AdminTheme.Api.CrestTenant;
using TenantBulkAction = Crest.AdminTheme.Api.TenantBulkAction;
using TenantCatalog = Crest.AdminTheme.Api.TenantCatalog;
using TenantEditor = Crest.AdminTheme.Api.TenantEditor;
using TenantChoice = Crest.AdminTheme.Api.TenantChoice;
using TenantFieldError = Crest.AdminTheme.Api.TenantFieldError;
using TenantSaveResult = Crest.AdminTheme.Api.TenantSaveResult;
using TenantSetupRequest = Crest.AdminTheme.Api.TenantSetupRequest;
using WireTenantForm = Crest.AdminTheme.Api.TenantForm;
using Crest.Tenants.Services;

namespace Crest.Controllers;

// Crest.Tenants is deliberately NOT declared in Crest's manifest
// Dependencies (unlike Contents/Media/Localization/etc. - see Manifest.cs) even though
// this controller is reachable whenever the always-enabled Crest feature is present:
// the Tenants feature is DefaultTenantOnly, so a hard dependency would be invalid (and
// silently unsatisfiable) on every non-default tenant. IOptions<TenantsOptions> below is
// safe without it regardless - DI always resolves it to a default-constructed instance
// (TenantRemovalAllowed defaults to false, the conservative/correct value) even when the
// Tenants feature is off, so there's no crash and no incorrect-permissive fallback to
// guard against. GetDefaultTenantAccessAsync already restricts this whole controller to
// the default tenant, where Tenants is normally enabled anyway.
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/tenants")]
public sealed class TenantsController(IRequestAccess requestAccess) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<TenantCatalog>> ListAsync(
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] string? state = null,
        [FromQuery] string? orderBy = null)
    {
        var access = await GetDefaultTenantAccessAsync();
        if (access is null) return Forbid();

        var settings = access.GetRequiredService<IShellHost>().GetAllSettings();
        var entries = settings.Select(ToTenant);
        if (!string.IsNullOrWhiteSpace(search))
        {
            entries = entries.Where(tenant => tenant.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || tenant.RequestUrlHost?.Contains(search, StringComparison.OrdinalIgnoreCase) == true
                || tenant.RequestUrlPrefix?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);
        }
        if (!string.IsNullOrWhiteSpace(category)) entries = entries.Where(tenant => string.Equals(tenant.Category, category, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(state) && !string.Equals(state, "All", StringComparison.OrdinalIgnoreCase)) entries = entries.Where(tenant => string.Equals(tenant.State, state, StringComparison.OrdinalIgnoreCase));

        entries = string.Equals(orderBy, "State", StringComparison.OrdinalIgnoreCase)
            ? entries.OrderBy(tenant => tenant.State, StringComparer.OrdinalIgnoreCase).ThenBy(tenant => tenant.Name, StringComparer.OrdinalIgnoreCase)
            : entries.OrderBy(tenant => tenant.Name, StringComparer.OrdinalIgnoreCase);

        var categories = settings.Select(setting => setting["Category"])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var removalAllowed = access.GetRequiredService<IOptions<TenantsOptions>>().Value.TenantRemovalAllowed;
        return Ok(new TenantCatalog(entries.ToArray(), categories, removalAllowed));
    }

    // Create, edit and setup (step 7: the MVC Tenants Create/Edit screens are retired). The rules
    // are TenantAdministration's, shared with the stock tenants API.
    [HttpGet("editor")]
    public async Task<ActionResult<TenantEditor>> GetEditorAsync([FromQuery] string? name = null)
    {
        var access = await GetDefaultTenantAccessAsync();
        if (access is null) return Forbid();

        var state = await access.GetRequiredService<TenantAdministration>().GetEditorAsync(name);
        if (state is null) return NotFound();

        return Ok(new TenantEditor(
            ToWire(state.Form),
            state.IsNew,
            state.IsUninitialized,
            state.CanEditDatabase,
            state.DatabaseProviderPreset,
            state.DatabaseConfigurationPreset,
            state.RequireTablePrefix,
            state.HasTablePrefixPattern,
            state.HasSchemaPattern,
            state.ResolvedTablePrefix,
            state.ResolvedSchema,
            [.. state.Recipes.Select(ToChoice)],
            [.. state.FeatureProfiles.Select(ToChoice)],
            [.. state.DatabaseProviders.Select(ToChoice)]));
    }

    [HttpPost]
    public async Task<ActionResult<TenantSaveResult>> CreateAsync([FromBody] WireTenantForm form, [FromQuery] bool setupUrl = false)
    {
        var access = await GetDefaultTenantAccessAsync();
        if (access is null) return Forbid();

        var administration = access.GetRequiredService<TenantAdministration>();
        var result = await administration.CreateAsync(FromWire(form));
        return ToResponse(result, setupUrl && result.Settings is { } created ? administration.SetupUrl(HttpContext, created.Name) : null);
    }

    [HttpPut("{name}")]
    public async Task<ActionResult<TenantSaveResult>> EditAsync(string name, [FromBody] WireTenantForm form)
    {
        var access = await GetDefaultTenantAccessAsync();
        if (access is null) return Forbid();

        return ToResponse(await access.GetRequiredService<TenantAdministration>().EditAsync(name, FromWire(form)), null);
    }

    [HttpPost("{name}/setup")]
    public async Task<ActionResult<TenantSaveResult>> SetupAsync(string name, [FromBody] TenantSetupRequest request)
    {
        var access = await GetDefaultTenantAccessAsync();
        if (access is null) return Forbid();

        return ToResponse(await access.GetRequiredService<TenantAdministration>().SetupAsync(name, new TenantSetupForm
        {
            SiteName = request.SiteName,
            UserName = request.UserName,
            Email = request.Email,
            Password = request.Password,
            RecipeName = request.RecipeName,
            SiteTimeZone = request.SiteTimeZone,
            DatabaseProvider = request.DatabaseProvider,
            ConnectionString = request.ConnectionString,
            TablePrefix = request.TablePrefix,
            Schema = request.Schema,
        }), null);
    }

    [HttpGet("{name}/setup-url")]
    public async Task<IActionResult> GetSetupUrlAsync(string name)
    {
        var access = await GetDefaultTenantAccessAsync();
        if (access is null) return Forbid();

        var url = access.GetRequiredService<TenantAdministration>().SetupUrl(HttpContext, name);
        return url is null ? NotFound() : Ok(new { url });
    }

    private ActionResult<TenantSaveResult> ToResponse(TenantResult result, string? setupUrl)
    {
        var response = new TenantSaveResult(
            result.Succeeded,
            [.. result.Errors.Select(error => new TenantFieldError(error.Key, error.Message))],
            result.Settings is { } settings ? ToTenant(settings) : null,
            setupUrl);
        return result.Succeeded ? Ok(response) : BadRequest(response);
    }

    private static TenantChoice ToChoice(TenantOption option) => new(option.Value, option.Text, option.HasConnectionString);

    private static WireTenantForm ToWire(Crest.Tenants.Services.TenantForm form) => new()
    {
        Name = form.Name,
        Description = form.Description,
        Category = form.Category,
        RequestUrlHost = form.RequestUrlHost,
        RequestUrlPrefix = form.RequestUrlPrefix,
        DatabaseProvider = form.DatabaseProvider,
        ConnectionString = form.ConnectionString,
        TablePrefix = form.TablePrefix,
        Schema = form.Schema,
        RecipeName = form.RecipeName,
        FeatureProfiles = form.FeatureProfiles ?? [],
    };

    private static Crest.Tenants.Services.TenantForm FromWire(WireTenantForm form) => new()
    {
        Name = form.Name?.Trim(),
        Description = form.Description,
        Category = form.Category,
        RequestUrlHost = form.RequestUrlHost,
        RequestUrlPrefix = form.RequestUrlPrefix,
        DatabaseProvider = form.DatabaseProvider,
        ConnectionString = form.ConnectionString,
        TablePrefix = form.TablePrefix,
        Schema = form.Schema,
        RecipeName = form.RecipeName,
        FeatureProfiles = form.FeatureProfiles ?? [],
    };

    [HttpPost("{name}/enable")]
    public Task<ActionResult<CrestTenant>> EnableAsync(string name) => ChangeStateAsync(name, "Enable");

    [HttpPost("{name}/disable")]
    public Task<ActionResult<CrestTenant>> DisableAsync(string name) => ChangeStateAsync(name, "Disable");

    [HttpPost("bulk")]
    public async Task<ActionResult<CrestTenant[]>> BulkAsync([FromBody] TenantBulkAction request)
    {
        if (!string.Equals(request.Action, "Enable", StringComparison.OrdinalIgnoreCase) && !string.Equals(request.Action, "Disable", StringComparison.OrdinalIgnoreCase)) return BadRequest("Only Enable and Disable are valid tenant bulk actions.");
        var access = await GetDefaultTenantAccessAsync();
        if (access is null) return Forbid();

        var host = access.GetRequiredService<IShellHost>();
        var updated = new List<CrestTenant>();
        foreach (var name in request.Names?.Distinct(StringComparer.OrdinalIgnoreCase) ?? [])
        {
            if (!host.TryGetSettings(name, out var settings)) continue;
            if (string.Equals(request.Action, "Enable", StringComparison.OrdinalIgnoreCase) && settings.IsDisabled())
            {
                await host.UpdateShellSettingsAsync(settings.AsRunning());
                updated.Add(ToTenant(settings.AsRunning()));
            }
            else if (string.Equals(request.Action, "Disable", StringComparison.OrdinalIgnoreCase) && !settings.IsDefaultShell() && settings.IsRunning())
            {
                await host.UpdateShellSettingsAsync(settings.AsDisabled());
                updated.Add(ToTenant(settings.AsDisabled()));
            }
        }
        return Ok(updated.ToArray());
    }

    [HttpPost("{name}/reload")]
    public async Task<IActionResult> ReloadAsync(string name)
    {
        var access = await GetDefaultTenantAccessAsync();
        if (access is null) return Forbid();
        var host = access.GetRequiredService<IShellHost>();
        if (!host.TryGetSettings(name, out var settings)) return NotFound();
        await host.ReloadShellContextAsync(settings);
        return NoContent();
    }

    [HttpDelete("{name}")]
    public async Task<IActionResult> RemoveAsync(string name)
    {
        var access = await GetDefaultTenantAccessAsync();
        if (access is null) return Forbid();
        if (!access.GetRequiredService<IOptions<TenantsOptions>>().Value.TenantRemovalAllowed) return Forbid();

        var host = access.GetRequiredService<IShellHost>();
        if (!host.TryGetSettings(name, out var settings)) return NotFound();
        if (!settings.IsRemovable()) return BadRequest("Only Disabled or Uninitialized tenants can be removed.");

        var result = await access.GetRequiredService<IShellRemovalManager>().RemoveAsync(settings);
        return result.Success ? NoContent() : BadRequest(result.ErrorMessage);
    }

    private async Task<ActionResult<CrestTenant>> ChangeStateAsync(string name, string action)
    {
        var access = await GetDefaultTenantAccessAsync();
        if (access is null) return Forbid();
        var host = access.GetRequiredService<IShellHost>();
        if (!host.TryGetSettings(name, out var settings)) return NotFound();

        if (string.Equals(action, "Enable", StringComparison.OrdinalIgnoreCase))
        {
            if (!settings.IsDisabled()) return BadRequest("Only Disabled tenants can be enabled.");
            var enabled = settings.AsRunning();
            await host.UpdateShellSettingsAsync(enabled);
            return Ok(ToTenant(enabled));
        }

        if (settings.IsDefaultShell()) return BadRequest("The default tenant cannot be disabled.");
        if (!settings.IsRunning()) return BadRequest("Only Running tenants can be disabled.");
        var disabled = settings.AsDisabled();
        await host.UpdateShellSettingsAsync(disabled);
        return Ok(ToTenant(disabled));
    }

    private async Task<AuthorizedRequest?> GetDefaultTenantAccessAsync()
    {
        var access = await requestAccess.AuthorizeAsync(User, TenantsPermissions.ManageTenants);
        if (access is null || !access.GetRequiredService<ShellSettings>().IsDefaultShell()) return null;
        return access;
    }

    private static CrestTenant ToTenant(ShellSettings settings) => new(
        settings.Name,
        settings.State.ToString(),
        settings["Category"],
        settings["Description"],
        settings.RequestUrlHost,
        settings.RequestUrlPrefix,
        settings.IsDefaultShell(),
        settings.IsRemovable());
}
