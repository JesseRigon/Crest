using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Grove.Abstractions.Setup;
using Grove.Data;
using Grove.Email;
using Grove.Environment.Shell;
using Grove.FileStorage;
using Grove.Environment.Shell.Removing;
using Grove.Modules;
using Grove.Mvc.ModelBinding;
using Grove.Mvc.Utilities;
using Grove.Recipes.Models;
using Grove.Setup.Services;
using Grove.Tenants.Models;
using Grove.Tenants.Services;
using Grove.Tenants.ViewModels;

namespace Grove.Tenants.Controllers;

[Route("api/tenants")]
[ApiController]
[Authorize, IgnoreAntiforgeryToken, AllowAnonymous]
public sealed class TenantApiController : ControllerBase
{
    private readonly IShellHost _shellHost;
    private readonly ShellSettings _currentShellSettings;
    private readonly IShellRemovalManager _shellRemovalManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShellSettingsManager _shellSettingsManager;
    private readonly IDataProtectionProvider _dataProtectorProvider;
    private readonly IClock _clock;
    private readonly TenantsOptions _tenantsOptions;
    private readonly Dictionary<string, DatabaseProvider> _databaseProviderLookup;
    private readonly ITenantValidator _tenantValidator;
    private readonly TenantDatabasePatternResolver _tenantDatabasePatternResolver;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public TenantApiController(
        IShellHost shellHost,
        ShellSettings currentShellSettings,
        IShellRemovalManager shellRemovalManager,
        IAuthorizationService authorizationService,
        IShellSettingsManager shellSettingsManager,
        IDataProtectionProvider dataProtectorProvider,
        IClock clock,
        IOptions<TenantsOptions> tenantsOptions,
        IEnumerable<DatabaseProvider> databaseProviders,
        ITenantValidator tenantValidator,
        TenantDatabasePatternResolver tenantDatabasePatternResolver,
        IStringLocalizer<TenantApiController> stringLocalizer,
        ILogger<TenantApiController> logger)
    {
        _shellHost = shellHost;
        _currentShellSettings = currentShellSettings;
        _shellRemovalManager = shellRemovalManager;
        _authorizationService = authorizationService;
        _dataProtectorProvider = dataProtectorProvider;
        _shellSettingsManager = shellSettingsManager;
        _clock = clock;
        _tenantsOptions = tenantsOptions.Value;
        _databaseProviderLookup = databaseProviders.ToDictionary(provider => provider.Value, StringComparer.OrdinalIgnoreCase);
        _tenantValidator = tenantValidator;
        _tenantDatabasePatternResolver = tenantDatabasePatternResolver;
        S = stringLocalizer;
        _logger = logger;
    }

    [HttpPost]
    [Route("create")]
    [EndpointName("ApiCreateTenant")]
    public async Task<IActionResult> Create(TenantApiModel model)
    {
        if (!_currentShellSettings.IsDefaultShell())
        {
            return Forbid();
        }

        if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageTenants))
        {
            return this.ChallengeOrForbid(PlatformConstants.AuthenticationSchemes.Api);
        }

        _ = _shellHost.TryGetSettings(model.Name, out var settings);
        ApplyPresetDatabaseConfiguration(model);
        ApplyConfiguredDatabasePatterns(model);
        try
        {
            await ValidateModelAsync(model, isNewTenant: settings is null);
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            _logger.LogError(ex, "An error occurred while validating the tenant '{TenantName}'.", model.Name);
            ModelState.AddModelError(string.Empty, S["An error occurred while validating the tenant database settings."]);
        }

        if (ModelState.IsValid)
        {
            if (!model.IsNewTenant)
            {
                // Site already exists, return 201 for idempotency purposes.

                var token = CreateSetupToken(settings);

                return Created(GetEncodedUrl(settings, token), null);
            }

            try
            {
                // Creates a default shell settings based on the configuration.
                using var shellSettings = _shellSettingsManager
                    .CreateDefaultSettings()
                    .AsUninitialized()
                    .AsDisposable();

                shellSettings.Name = model.Name;
                shellSettings.RequestUrlHost = model.RequestUrlHost;
                shellSettings.RequestUrlPrefix = model.RequestUrlPrefix;

                shellSettings["Category"] = model.Category;
                shellSettings["Description"] = model.Description;
                shellSettings["ConnectionString"] = model.ConnectionString;
                shellSettings["TablePrefix"] = model.TablePrefix;
                shellSettings["Schema"] = model.Schema;
                shellSettings["DatabaseProvider"] = model.DatabaseProvider;
                shellSettings["Secret"] = Guid.NewGuid().ToString();
                shellSettings["RecipeName"] = model.RecipeName;
                shellSettings["FeatureProfile"] = string.Join(',', model.FeatureProfiles ?? []);

                await _shellHost.UpdateShellSettingsAsync(shellSettings);
                var reloadedSettings = _shellHost.GetSettings(shellSettings.Name);

                var token = CreateSetupToken(reloadedSettings);

                return Ok(GetEncodedUrl(reloadedSettings, token));
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                _logger.LogError(ex, "An error occurred while saving the tenant '{TenantName}'.", model.Name);
                ModelState.AddModelError(string.Empty, S["An error occurred while saving the tenant settings."]);
            }
        }

        return BadRequest(ModelState);
    }

    [HttpPost]
    [Route("edit")]
    [EndpointName("ApiEditTenant")]
    public async Task<IActionResult> Edit(TenantApiModel model)
    {
        if (!_currentShellSettings.IsDefaultShell())
        {
            return Forbid();
        }

        if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageTenants))
        {
            return this.ChallengeOrForbid(PlatformConstants.AuthenticationSchemes.Api);
        }

        ApplyPresetDatabaseConfiguration(model);
        ApplyConfiguredDatabasePatterns(model);
        if (ModelState.IsValid)
        {
            try
            {
                await ValidateModelAsync(model, isNewTenant: false);
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                _logger.LogError(ex, "An error occurred while validating the tenant '{TenantName}'.", model.Name);
                ModelState.AddModelError(string.Empty, S["An error occurred while validating the tenant database settings."]);
            }
        }

        if (!_shellHost.TryGetSettings(model.Name, out var shellSettings))
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                shellSettings["Description"] = model.Description;
                shellSettings["Category"] = model.Category;
                shellSettings.RequestUrlPrefix = model.RequestUrlPrefix;
                shellSettings.RequestUrlHost = model.RequestUrlHost;
                shellSettings["FeatureProfile"] = string.Join(',', model.FeatureProfiles ?? []);

                if (shellSettings.IsUninitialized())
                {
                    shellSettings["DatabaseProvider"] = model.DatabaseProvider;
                    shellSettings["TablePrefix"] = model.TablePrefix;
                    shellSettings["Schema"] = model.Schema;
                    shellSettings["ConnectionString"] = model.ConnectionString;
                    shellSettings["RecipeName"] = model.RecipeName;
                    shellSettings["Secret"] = Guid.NewGuid().ToString();
                }

                await _shellHost.UpdateShellSettingsAsync(shellSettings);

                return Ok();
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                _logger.LogError(ex, "An error occurred while saving the tenant '{TenantName}'.", model.Name);
                ModelState.AddModelError(string.Empty, S["An error occurred while saving the tenant settings."]);
            }
        }

        return BadRequest(ModelState);
    }

    [HttpPost]
    [Route("disable/{tenantName}")]
    [EndpointName("ApiDisableTenant")]
    public async Task<IActionResult> Disable(string tenantName)
    {
        if (!_currentShellSettings.IsDefaultShell())
        {
            return Forbid();
        }

        if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageTenants))
        {
            return this.ChallengeOrForbid(PlatformConstants.AuthenticationSchemes.Api);
        }

        if (!_shellHost.TryGetSettings(tenantName, out var shellSettings))
        {
            return NotFound();
        }

        if (!shellSettings.IsRunning())
        {
            return BadRequest(S["You can only disable a Running tenant."]);
        }

        await _shellHost.UpdateShellSettingsAsync(shellSettings.AsDisabled());

        return Ok();
    }

    [HttpPost]
    [Route("enable/{tenantName}")]
    [EndpointName("ApiEnableTenant")]
    public async Task<IActionResult> Enable(string tenantName)
    {
        if (!_currentShellSettings.IsDefaultShell())
        {
            return Forbid();
        }

        if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageTenants))
        {
            return this.ChallengeOrForbid(PlatformConstants.AuthenticationSchemes.Api);
        }

        if (!_shellHost.TryGetSettings(tenantName, out var shellSettings))
        {
            return NotFound();
        }

        if (!shellSettings.IsDisabled())
        {
            return BadRequest(S["You can only enable a Disabled tenant."]);
        }

        await _shellHost.UpdateShellSettingsAsync(shellSettings.AsRunning());

        return Ok();
    }

    [HttpPost]
    [Route("remove/{tenantName}")]
    [EndpointName("ApiRemoveTenant")]
    public async Task<IActionResult> Remove(string tenantName)
    {
        if (!_currentShellSettings.IsDefaultShell() || !_tenantsOptions.TenantRemovalAllowed)
        {
            return Forbid();
        }

        if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageTenants))
        {
            return this.ChallengeOrForbid(PlatformConstants.AuthenticationSchemes.Api);
        }

        if (!_shellHost.TryGetSettings(tenantName, out var shellSettings))
        {
            return NotFound();
        }

        if (!shellSettings.IsRemovable())
        {
            return BadRequest(S["You can only remove a 'Disabled' or an 'Uninitialized' tenant."]);
        }

        var context = await _shellRemovalManager.RemoveAsync(shellSettings);
        if (!context.Success)
        {
            return Problem(
                title: S["An error occurred while removing the tenant '{0}'.", tenantName],
                detail: context.ErrorMessage,
                statusCode: (int)HttpStatusCode.BadRequest);
        }

        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning("The tenant '{TenantName}' was removed.", shellSettings.Name);
        }

        return Ok();
    }

    [HttpPost]
    [Route("setup")]
    [EndpointName("ApiSetupTenant")]
    public async Task<ActionResult> Setup(SetupApiViewModel model)
    {
        if (!_currentShellSettings.IsDefaultShell())
        {
            return this.ChallengeOrForbid(PlatformConstants.AuthenticationSchemes.Api);
        }

        if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageTenants))
        {
            return this.ChallengeOrForbid(PlatformConstants.AuthenticationSchemes.Api);
        }

        if (!ModelState.IsValid)
        {
            return BadRequest();
        }

        // The setup itself is the tenant administration's, shared with the native Tenants page.
        var result = await HttpContext.RequestServices.GetRequiredService<TenantAdministration>().SetupAsync(model.Name, new TenantSetupForm
        {
            SiteName = model.SiteName,
            UserName = model.UserName,
            Email = model.Email,
            Password = model.Password,
            RecipeName = model.RecipeName,
            Recipe = model.Recipe,
            SiteTimeZone = model.SiteTimeZone,
            DatabaseProvider = model.DatabaseProvider,
            ConnectionString = model.ConnectionString,
            TablePrefix = model.TablePrefix,
            Schema = model.Schema,
        });

        if (result.Succeeded && result.ExecutionId is null && result.Settings is { } running)
        {
            return Created(GetEncodedUrl(running, null), null);
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(error.Key, error.Message);
        }

        return result.Succeeded ? Ok(result.ExecutionId) : BadRequest(ModelState);
    }

    private string GetEncodedUrl(ShellSettings shellSettings, string token)
    {
        var host = shellSettings.RequestUrlHosts.FirstOrDefault();
        var hostString = host != null ? new HostString(host) : Request.Host;

        var pathString = HttpContext.Features.Get<ShellContextFeature>()?.OriginalPathBase ?? PathString.Empty;
        if (!string.IsNullOrEmpty(shellSettings.RequestUrlPrefix))
        {
            pathString = pathString.Add('/' + shellSettings.RequestUrlPrefix);
        }

        var queryString = QueryString.Empty;
        if (!string.IsNullOrEmpty(token))
        {
            queryString = QueryString.Create("token", token);
        }

        return $"{Request.Scheme}://{hostString + pathString + queryString}";
    }

    private string CreateSetupToken(ShellSettings shellSettings)
    {
        // Create a public url to setup the new tenant
        var dataProtector = _dataProtectorProvider.CreateProtector("Tokens").ToTimeLimitedDataProtector();
        var token = dataProtector.Protect(shellSettings["Secret"], _clock.UtcNow.Add(new TimeSpan(24, 0, 0)));

        return token;
    }

    private async Task ValidateModelAsync(TenantApiModel model, bool isNewTenant)
    {
        model.IsNewTenant = isNewTenant;

        ModelState.AddModelErrors(await _tenantValidator.ValidateAsync(model));
    }

    private void ApplyPresetDatabaseConfiguration(TenantModelBase model)
    {
        var presetDatabaseConfiguration = GetPresetDatabaseConfiguration();
        if (!presetDatabaseConfiguration.HasDatabaseProviderPreset)
        {
            return;
        }

        model.DatabaseProvider = presetDatabaseConfiguration.DatabaseProvider;

        if (presetDatabaseConfiguration.HasConnectionStringPreset)
        {
            model.ConnectionString = presetDatabaseConfiguration.ConnectionString;
            model.Schema = presetDatabaseConfiguration.Schema;
        }
    }

    private void ApplyConfiguredDatabasePatterns(TenantModelBase model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return;
        }

        _tenantDatabasePatternResolver.Apply(model);
    }

    private (bool HasDatabaseProviderPreset, bool HasConnectionStringPreset, string DatabaseProvider, string ConnectionString, string Schema) GetPresetDatabaseConfiguration()
    {
        // Read from the root application configuration, not from the current
        // shell settings which may have tenant-specific overrides (e.g., the
        // Default tenant was set up with SQLite while root config says SqlConnection).
        using var defaultSettings = _shellSettingsManager.CreateDefaultSettings().AsDisposable();
        var databaseProvider = defaultSettings["DatabaseProvider"];
        var connectionString = defaultSettings["ConnectionString"];

        if (string.IsNullOrEmpty(databaseProvider) ||
            !_databaseProviderLookup.TryGetValue(databaseProvider, out var provider))
        {
            return (false, false, null, null, null);
        }

        return (
            true,
            !provider.HasConnectionString || !string.IsNullOrWhiteSpace(connectionString),
            databaseProvider,
            connectionString,
            defaultSettings["Schema"]);
    }
}
