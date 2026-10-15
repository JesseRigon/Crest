using System.Text.Json;
using Grove.Api;
using Grove.Data;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Removing;
using Grove.Infrastructure.Setup.Services;
using Grove.Infrastructure.Tenants.Models;
using Grove.Infrastructure.Tenants.Services;
using Grove.Infrastructure.Tenants.ViewModels;
using Grove.Modules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Grove.Infrastructure.Tenants.Api;

/// <summary>
/// The stock tenants API for remote callers (<c>api/tenants</c>: create, edit, enable, disable,
/// remove, setup), authenticated with the Api scheme. Anonymous at the endpoint and exempt from
/// antiforgery: every handler checks ManageTenants itself and challenges the Api scheme. The
/// setup goes through <see cref="TenantAdministration"/>, shared with <see cref="TenantsApi"/>.
/// </summary>
public sealed class TenantApi : ApiContributor
{
    public override string Name => "TenantsRemote";

    public override string FeatureId => "Grove.Infrastructure.Tenants";

    public override string RoutePrefix => "api/tenants";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("").AllowAnonymous().SkipApiAntiforgery();
        routes.MapPost("create", (HttpContext http, [FromBody] TenantApiModel model) => ApiHandlers.For<TenantApiHandlers>(http).CreateAsync(model))
            .WithName("ApiCreateTenant");
        routes.MapPost("edit", (HttpContext http, [FromBody] TenantApiModel model) => ApiHandlers.For<TenantApiHandlers>(http).EditAsync(model))
            .WithName("ApiEditTenant");
        routes.MapPost("disable/{tenantName}", (HttpContext http, [FromRoute] string tenantName) => ApiHandlers.For<TenantApiHandlers>(http).DisableAsync(tenantName))
            .WithName("ApiDisableTenant");
        routes.MapPost("enable/{tenantName}", (HttpContext http, [FromRoute] string tenantName) => ApiHandlers.For<TenantApiHandlers>(http).EnableAsync(tenantName))
            .WithName("ApiEnableTenant");
        routes.MapPost("remove/{tenantName}", (HttpContext http, [FromRoute] string tenantName) => ApiHandlers.For<TenantApiHandlers>(http).RemoveAsync(tenantName))
            .WithName("ApiRemoveTenant");
        routes.MapPost("setup", (HttpContext http, [FromBody] SetupApiViewModel model) => ApiHandlers.For<TenantApiHandlers>(http).SetupAsync(model))
            .WithName("ApiSetupTenant");
    }
}

public sealed class TenantApiHandlers : ApiHandlers
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

    public TenantApiHandlers(
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
        IStringLocalizer<TenantApiHandlers> stringLocalizer,
        ILogger<TenantApiHandlers> logger)
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

    public async Task<IResult> CreateAsync(TenantApiModel model)
    {
        if (!_currentShellSettings.IsDefaultShell())
        {
            return Forbid();
        }

        if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageTenants))
        {
            return ChallengeOrForbid();
        }

        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        if (RequireName(model, errors))
        {
            return Validation(errors);
        }

        _ = _shellHost.TryGetSettings(model.Name, out var settings);
        ApplyPresetDatabaseConfiguration(model);
        ApplyConfiguredDatabasePatterns(model);
        try
        {
            await ValidateModelAsync(model, isNewTenant: settings is null, errors);
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            _logger.LogError(ex, "An error occurred while validating the tenant '{TenantName}'.", model.Name);
            AddError(errors, string.Empty, S["An error occurred while validating the tenant database settings."]);
        }

        if (errors.Count == 0)
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
                AddError(errors, string.Empty, S["An error occurred while saving the tenant settings."]);
            }
        }

        return Validation(errors);
    }

    public async Task<IResult> EditAsync(TenantApiModel model)
    {
        if (!_currentShellSettings.IsDefaultShell())
        {
            return Forbid();
        }

        if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageTenants))
        {
            return ChallengeOrForbid();
        }

        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        if (RequireName(model, errors))
        {
            return Validation(errors);
        }

        ApplyPresetDatabaseConfiguration(model);
        ApplyConfiguredDatabasePatterns(model);
        try
        {
            await ValidateModelAsync(model, isNewTenant: false, errors);
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            _logger.LogError(ex, "An error occurred while validating the tenant '{TenantName}'.", model.Name);
            AddError(errors, string.Empty, S["An error occurred while validating the tenant database settings."]);
        }

        if (!_shellHost.TryGetSettings(model.Name, out var shellSettings))
        {
            return NotFound();
        }

        if (errors.Count == 0)
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
                AddError(errors, string.Empty, S["An error occurred while saving the tenant settings."]);
            }
        }

        return Validation(errors);
    }

    public async Task<IResult> DisableAsync(string tenantName)
    {
        if (!_currentShellSettings.IsDefaultShell())
        {
            return Forbid();
        }

        if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageTenants))
        {
            return ChallengeOrForbid();
        }

        if (!_shellHost.TryGetSettings(tenantName, out var shellSettings))
        {
            return NotFound();
        }

        if (!shellSettings.IsRunning())
        {
            return BadRequest(S["You can only disable a Running tenant."].Value);
        }

        await _shellHost.UpdateShellSettingsAsync(shellSettings.AsDisabled());

        return Ok();
    }

    public async Task<IResult> EnableAsync(string tenantName)
    {
        if (!_currentShellSettings.IsDefaultShell())
        {
            return Forbid();
        }

        if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageTenants))
        {
            return ChallengeOrForbid();
        }

        if (!_shellHost.TryGetSettings(tenantName, out var shellSettings))
        {
            return NotFound();
        }

        if (!shellSettings.IsDisabled())
        {
            return BadRequest(S["You can only enable a Disabled tenant."].Value);
        }

        await _shellHost.UpdateShellSettingsAsync(shellSettings.AsRunning());

        return Ok();
    }

    public async Task<IResult> RemoveAsync(string tenantName)
    {
        if (!_currentShellSettings.IsDefaultShell() || !_tenantsOptions.TenantRemovalAllowed)
        {
            return Forbid();
        }

        if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageTenants))
        {
            return ChallengeOrForbid();
        }

        if (!_shellHost.TryGetSettings(tenantName, out var shellSettings))
        {
            return NotFound();
        }

        if (!shellSettings.IsRemovable())
        {
            return BadRequest(S["You can only remove a 'Disabled' or an 'Uninitialized' tenant."].Value);
        }

        var context = await _shellRemovalManager.RemoveAsync(shellSettings);
        if (!context.Success)
        {
            return Problem(
                title: S["An error occurred while removing the tenant '{0}'.", tenantName],
                detail: context.ErrorMessage,
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning("The tenant '{TenantName}' was removed.", shellSettings.Name);
        }

        return Ok();
    }

    public async Task<IResult> SetupAsync(SetupApiViewModel model)
    {
        if (!_currentShellSettings.IsDefaultShell())
        {
            return ChallengeOrForbid();
        }

        if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageTenants))
        {
            return ChallengeOrForbid();
        }

        // The [Required] members of the request (MVC's automatic model validation answered these).
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (key, value) in new[]
        {
            (nameof(SetupApiViewModel.Name), model.Name),
            (nameof(SetupApiViewModel.SiteName), model.SiteName),
            (nameof(SetupApiViewModel.UserName), model.UserName),
            (nameof(SetupApiViewModel.Email), model.Email),
        })
        {
            if (string.IsNullOrEmpty(value))
            {
                AddError(errors, key, $"The {key} field is required.");
            }
        }

        if (errors.Count > 0)
        {
            return Validation(errors);
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
            AddError(errors, error.Key, error.Message);
        }

        return result.Succeeded ? Ok(result.ExecutionId) : Validation(errors);
    }

    // A 403 for a signed-in caller, a challenge of the Api scheme for an anonymous one.
    private IResult ChallengeOrForbid() =>
        User?.Identity?.IsAuthenticated ?? false
            ? Results.Forbid(authenticationSchemes: [PlatformConstants.AuthenticationSchemes.Api])
            : Results.Challenge(authenticationSchemes: [PlatformConstants.AuthenticationSchemes.Api]);

    private static bool RequireName(TenantModelBase model, Dictionary<string, List<string>> errors)
    {
        if (!string.IsNullOrEmpty(model?.Name))
        {
            return false;
        }

        AddError(errors, nameof(TenantModelBase.Name), "The Name field is required.");
        return true;
    }

    private static void AddError(Dictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var messages))
        {
            errors[key] = messages = [];
        }

        messages.Add(message);
    }

    // Keyed by the member's wire name (docs/MVC/api.md › The error contract).
    private static IResult Validation(Dictionary<string, List<string>> errors) =>
        ApiProblems.Validation(errors.ToDictionary(
            entry => string.IsNullOrEmpty(entry.Key) ? entry.Key : JsonNamingPolicy.CamelCase.ConvertName(entry.Key),
            entry => entry.Value.ToArray()));

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

    private async Task ValidateModelAsync(TenantApiModel model, bool isNewTenant, Dictionary<string, List<string>> errors)
    {
        model.IsNewTenant = isNewTenant;

        foreach (var error in await _tenantValidator.ValidateAsync(model))
        {
            AddError(errors, error.Key, error.Message);
        }
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
