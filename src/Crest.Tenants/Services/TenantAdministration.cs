#nullable enable

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Crest.Abstractions.Setup;
using Crest.Data;
using Crest.Email;
using Crest.Environment.Shell;
using Crest.FileStorage;
using Crest.Modules;
using Crest.Recipes.Models;
using Crest.Recipes.Services;
using Crest.Setup.Services;
using Crest.Tenants.Models;
using Crest.Tenants.ViewModels;

namespace Crest.Tenants.Services;

/// <summary>A tenant's editable settings, as the create and edit forms send them.</summary>
public sealed class TenantForm : TenantModelBase;

/// <summary>A choice the editor offers (a recipe, a feature profile, a database provider).</summary>
public sealed record TenantOption(string Value, string Text, bool HasConnectionString = false);

/// <summary>A refusal, keyed by the field it concerns ("" for the form as a whole).</summary>
public sealed record TenantError(string Key, string Message);

/// <summary>What the editor shows: the form, and what of it may be changed.</summary>
public sealed record TenantEditorState(
    TenantForm Form,
    bool IsNew,
    bool IsUninitialized,
    bool CanEditDatabase,
    bool DatabaseProviderPreset,
    bool DatabaseConfigurationPreset,
    bool RequireTablePrefix,
    bool HasTablePrefixPattern,
    bool HasSchemaPattern,
    string? ResolvedTablePrefix,
    string? ResolvedSchema,
    IReadOnlyList<TenantOption> Recipes,
    IReadOnlyList<TenantOption> FeatureProfiles,
    IReadOnlyList<TenantOption> DatabaseProviders);

/// <summary>The outcome of a create, edit or setup.</summary>
public sealed record TenantResult(bool Succeeded, IReadOnlyList<TenantError> Errors, ShellSettings? Settings = null, string? ExecutionId = null)
{
    public static TenantResult Refused(string key, string message) => new(false, [new TenantError(key, message)]);
}

/// <summary>Setting up an uninitialized tenant: the site, its first administrator, the recipe,
/// and the database settings the tenant record did not preset.</summary>
public sealed class TenantSetupForm
{
    public string? SiteName { get; set; }

    public string? UserName { get; set; }

    public string? Email { get; set; }

    public string? Password { get; set; }

    public string? RecipeName { get; set; }

    /// <summary>A JSON recipe, when no recipe name is given (the API's custom recipe).</summary>
    public string? Recipe { get; set; }

    public string? SiteTimeZone { get; set; }

    public string? DatabaseProvider { get; set; }

    public string? ConnectionString { get; set; }

    public string? TablePrefix { get; set; }

    public string? Schema { get; set; }
}

/// <summary>
/// Tenant creation, editing and setup, for every surface that offers them: the native Tenants
/// page (through <c>api/crest/tenants</c>) and the stock tenants API (<c>api/tenants</c>). The
/// caller has authorized ManageTenants on the default tenant; this class only applies the
/// rules (validation, database presets and patterns, what an initialized tenant may still
/// change) - the ones the retired MVC Create/Edit screens applied.
/// </summary>
public sealed class TenantAdministration(
    IShellHost shellHost,
    IShellSettingsManager shellSettingsManager,
    IEnumerable<IRecipeHarvester> recipeHarvesters,
    IEnumerable<DatabaseProvider> databaseProviders,
    IOptions<TenantsOptions> tenantsOptions,
    TenantDatabasePatternResolver patternResolver,
    ITenantValidator validator,
    IDataProtectionProvider dataProtectionProvider,
    IClock clock,
    IServiceProvider services,
    IStringLocalizer<TenantAdministration> stringLocalizer,
    ILogger<TenantAdministration> logger)
{
    private readonly IStringLocalizer S = stringLocalizer;
    private readonly Dictionary<string, DatabaseProvider> _providers =
        databaseProviders.ToDictionary(provider => provider.Value, StringComparer.OrdinalIgnoreCase);

    /// <summary>The create form (<paramref name="name"/> null) or a tenant's edit form; null when the tenant does not exist.</summary>
    public async Task<TenantEditorState?> GetEditorAsync(string? name)
    {
        TenantForm form;
        var isNew = string.IsNullOrWhiteSpace(name);
        var isUninitialized = true;
        if (isNew)
        {
            using var defaults = shellSettingsManager.CreateDefaultSettings().AsUninitialized().AsDisposable();
            form = new TenantForm
            {
                RequestUrlHost = defaults.RequestUrlHost,
                RequestUrlPrefix = defaults.RequestUrlPrefix,
                RecipeName = defaults["RecipeName"],
                FeatureProfiles = defaults.GetFeatureProfiles(),
                DatabaseProvider = defaults["DatabaseProvider"],
                ConnectionString = defaults["ConnectionString"],
                TablePrefix = defaults["TablePrefix"],
                Schema = defaults["Schema"],
            };
        }
        else
        {
            if (!shellHost.TryGetSettings(name!, out var settings))
            {
                return null;
            }

            isUninitialized = settings.IsUninitialized();
            form = new TenantForm
            {
                Name = settings.Name,
                Category = settings["Category"],
                Description = settings["Description"],
                RequestUrlHost = settings.RequestUrlHost,
                RequestUrlPrefix = settings.RequestUrlPrefix,
                FeatureProfiles = settings.GetFeatureProfiles(),
            };

            // The database settings of an initialized tenant are its database: never shown,
            // never changed. An uninitialized one shows them, the password redacted.
            if (isUninitialized)
            {
                form.DatabaseProvider = settings["DatabaseProvider"];
                form.TablePrefix = settings["TablePrefix"];
                form.Schema = settings["Schema"];
                form.ConnectionString = TenantConnectionStringRedactor.RedactPassword(settings["ConnectionString"]);
                form.RecipeName = settings["RecipeName"];
            }
        }

        var presets = ApplyPresets(form, isUninitialized);
        var options = tenantsOptions.Value;
        var resolution = string.IsNullOrWhiteSpace(form.Name) ? null : patternResolver.Apply(form);

        return new TenantEditorState(
            form,
            isNew,
            isUninitialized,
            CanEditDatabase: isUninitialized && !presets.Configuration,
            DatabaseProviderPreset: presets.Provider,
            DatabaseConfigurationPreset: presets.Configuration,
            RequireTablePrefix: options.RequireTablePrefix,
            HasTablePrefixPattern: !string.IsNullOrWhiteSpace(options.TablePrefixPattern),
            HasSchemaPattern: !string.IsNullOrWhiteSpace(options.SchemaPattern),
            ResolvedTablePrefix: resolution?.TablePrefix,
            ResolvedSchema: resolution?.Schema,
            Recipes: isUninitialized ? await SetupRecipesAsync() : [],
            FeatureProfiles: await FeatureProfilesAsync(),
            DatabaseProviders: [.. _providers.Values.Select(provider => new TenantOption(provider.Value, provider.Name, provider.HasConnectionString))]);
    }

    /// <summary>Creates an uninitialized tenant.</summary>
    public async Task<TenantResult> CreateAsync(TenantForm form)
    {
        ApplyPresets(form, isUninitialized: true);
        patternResolver.Apply(form);
        form.IsNewTenant = true;
        var errors = await ValidateAsync(form);
        if (errors.Count > 0)
        {
            return new TenantResult(false, errors);
        }

        try
        {
            using var settings = shellSettingsManager.CreateDefaultSettings().AsUninitialized().AsDisposable();
            settings.Name = form.Name;
            settings.RequestUrlHost = form.RequestUrlHost;
            settings.RequestUrlPrefix = form.RequestUrlPrefix;
            settings["Category"] = form.Category;
            settings["Description"] = form.Description;
            settings["ConnectionString"] = form.ConnectionString;
            settings["TablePrefix"] = form.TablePrefix;
            settings["Schema"] = form.Schema;
            settings["DatabaseProvider"] = form.DatabaseProvider;
            settings["Secret"] = Guid.NewGuid().ToString();
            settings["RecipeName"] = form.RecipeName;
            settings["FeatureProfile"] = string.Join(',', form.FeatureProfiles ?? []);

            await shellHost.UpdateShellSettingsAsync(settings);
            return new TenantResult(true, [], shellHost.TryGetSettings(form.Name, out var created) ? created : null);
        }
        catch (Exception exception) when (!exception.IsFatal())
        {
            logger.LogError(exception, "An error occurred while saving the tenant '{TenantName}'.", form.Name);
            return TenantResult.Refused(string.Empty, S["An error occurred while saving the tenant settings."]);
        }
    }

    /// <summary>
    /// Edits a tenant. An initialized tenant keeps its database settings and recipe whatever
    /// the form says; an uninitialized one takes them, a redacted connection string meaning
    /// "unchanged".
    /// </summary>
    public async Task<TenantResult> EditAsync(string name, TenantForm form)
    {
        if (!shellHost.TryGetSettings(name, out var settings))
        {
            return TenantResult.Refused(nameof(TenantForm.Name), S["The tenant '{0}' does not exist.", name]);
        }

        form.Name = settings.Name;
        var isUninitialized = settings.IsUninitialized();
        if (isUninitialized)
        {
            form.ConnectionString = TenantConnectionStringRedactor.RestoreIfRedacted(settings["ConnectionString"], form.ConnectionString);
        }

        ApplyPresets(form, isUninitialized);
        patternResolver.Apply(form);
        form.IsNewTenant = false;
        var errors = await ValidateAsync(form);
        if (errors.Count > 0)
        {
            return new TenantResult(false, errors);
        }

        try
        {
            settings["Description"] = form.Description;
            settings["Category"] = form.Category;
            settings.RequestUrlPrefix = form.RequestUrlPrefix;
            settings.RequestUrlHost = form.RequestUrlHost;
            settings["FeatureProfile"] = string.Join(',', form.FeatureProfiles ?? []);

            if (isUninitialized)
            {
                settings["DatabaseProvider"] = form.DatabaseProvider;
                settings["TablePrefix"] = form.TablePrefix;
                settings["Schema"] = form.Schema;
                settings["ConnectionString"] = form.ConnectionString;
                settings["RecipeName"] = form.RecipeName;
                settings["Secret"] = Guid.NewGuid().ToString();
            }

            await shellHost.UpdateShellSettingsAsync(settings);
            return new TenantResult(true, [], settings);
        }
        catch (Exception exception) when (!exception.IsFatal())
        {
            logger.LogError(exception, "An error occurred while saving the tenant '{TenantName}'.", name);
            return TenantResult.Refused(string.Empty, S["An error occurred while saving the tenant settings."]);
        }
    }

    /// <summary>
    /// The tenant's own setup URL with a 24-hour token, for setting it up in that tenant's own
    /// setup page (the MVC "create and set up" action's redirect).
    /// </summary>
    public string? SetupUrl(HttpContext httpContext, string name)
    {
        if (!shellHost.TryGetSettings(name, out var settings) || !settings.IsUninitialized())
        {
            return null;
        }

        var protector = dataProtectionProvider.CreateProtector("Tokens").ToTimeLimitedDataProtector();
        var token = protector.Protect(settings["Secret"], clock.UtcNow.Add(TimeSpan.FromHours(24)));
        return httpContext.GetEncodedUrl(new ShellSettingsEntry { Name = settings.Name, ShellSettings = settings, Token = token });
    }

    /// <summary>
    /// Sets up an uninitialized tenant in place: the database settings the tenant record left
    /// open come from the form, the record's own and the host's presets win.
    /// </summary>
    public async Task<TenantResult> SetupAsync(string name, TenantSetupForm form)
    {
        var errors = new List<TenantError>();
        if (string.IsNullOrWhiteSpace(form.SiteName))
        {
            errors.Add(new TenantError(nameof(form.SiteName), S["The site name is required."]));
        }

        if (string.IsNullOrWhiteSpace(form.UserName))
        {
            errors.Add(new TenantError(nameof(form.UserName), S["The user name is required."]));
        }
        else if (services.GetService<IOptions<IdentityOptions>>()?.Value.User.AllowedUserNameCharacters is { Length: > 0 } allowed
            && form.UserName.Any(character => !allowed.Contains(character)))
        {
            errors.Add(new TenantError(nameof(form.UserName), S["User name '{0}' is invalid, can only contain letters or digits.", form.UserName]));
        }

        if (string.IsNullOrWhiteSpace(form.Email))
        {
            errors.Add(new TenantError(nameof(form.Email), S["The email is required."]));
        }
        else if (services.GetService<IEmailAddressValidator>() is { } emailValidator && !emailValidator.Validate(form.Email))
        {
            errors.Add(new TenantError(nameof(form.Email), S["The email is invalid."]));
        }

        if (errors.Count > 0)
        {
            return new TenantResult(false, errors);
        }

        if (!shellHost.TryGetSettings(name, out var settings))
        {
            return TenantResult.Refused(nameof(TenantForm.Name), S["Tenant not found: '{0}'", name]);
        }

        if (settings.IsRunning())
        {
            return new TenantResult(true, [], settings);
        }

        if (!settings.IsUninitialized())
        {
            return TenantResult.Refused(string.Empty, S["The tenant can't be setup."]);
        }

        var presets = GetPresets();
        var databaseProvider = presets.Provider ? presets.DatabaseProvider : Coalesce(settings["DatabaseProvider"], form.DatabaseProvider);
        if (string.IsNullOrEmpty(databaseProvider))
        {
            return TenantResult.Refused(nameof(form.DatabaseProvider), S["The database provider is not defined."]);
        }

        if (!_providers.TryGetValue(databaseProvider, out var provider))
        {
            return TenantResult.Refused(nameof(form.DatabaseProvider), S["The database provider is not supported."]);
        }

        var patterns = patternResolver.Resolve(settings);
        if (!string.IsNullOrEmpty(patterns.TablePrefixError))
        {
            errors.Add(new TenantError(nameof(form.TablePrefix), patterns.TablePrefixError));
        }

        if (!string.IsNullOrEmpty(patterns.SchemaError))
        {
            errors.Add(new TenantError(nameof(form.Schema), patterns.SchemaError));
        }

        if (errors.Count > 0)
        {
            return new TenantResult(false, errors);
        }

        var tablePrefix = patterns.HasTablePrefixPattern ? patterns.TablePrefix : Coalesce(settings["TablePrefix"], form.TablePrefix);
        var schema = patterns.HasSchemaPattern ? patterns.Schema : Coalesce(settings["Schema"], form.Schema);
        var connectionString = Coalesce(settings["ConnectionString"], form.ConnectionString);
        if (presets.Configuration)
        {
            schema = presets.Schema;
            connectionString = presets.ConnectionString;
        }

        if (provider.HasConnectionString && string.IsNullOrEmpty(connectionString))
        {
            return TenantResult.Refused(nameof(form.ConnectionString), S["The connection string is required for this database provider."]);
        }

        var setupService = services.GetRequiredService<ISetupService>();
        RecipeDescriptor? recipe;
        var recipeName = Coalesce(settings["RecipeName"], form.RecipeName);
        if (string.IsNullOrEmpty(recipeName))
        {
            if (string.IsNullOrWhiteSpace(form.Recipe))
            {
                return TenantResult.Refused(nameof(form.RecipeName), S["Either a 'recipe' file or 'RecipeName' is required."]);
            }

            var file = services.GetRequiredService<ITempDirectoryProvider>().GetTempFileName();
            await File.WriteAllTextAsync(file, form.Recipe);
            var fileProvider = new PhysicalFileProvider(Path.GetDirectoryName(file)!);
            recipe = new RecipeDescriptor
            {
                FileProvider = fileProvider,
                BasePath = string.Empty,
                RecipeFileInfo = fileProvider.GetFileInfo(Path.GetFileName(file)),
            };
        }
        else
        {
            recipe = (await setupService.GetSetupRecipesAsync())
                .FirstOrDefault(candidate => string.Equals(candidate.Name, recipeName, StringComparison.OrdinalIgnoreCase));
            if (recipe is null)
            {
                return TenantResult.Refused(nameof(form.RecipeName), S["Recipe '{0}' not found.", recipeName]);
            }
        }

        var context = new SetupContext
        {
            ShellSettings = settings,
            EnabledFeatures = null,
            Errors = new Dictionary<string, string>(),
            Recipe = recipe,
            // Setup reads absent values as "not given" (the stock setup page posts them the same way).
#pragma warning disable CS8604
            Properties = new Dictionary<string, object>
            {
                { SetupConstants.SiteName, form.SiteName },
                { SetupConstants.AdminUsername, form.UserName },
                { SetupConstants.AdminEmail, form.Email },
                { SetupConstants.AdminPassword, form.Password },
                { SetupConstants.SiteTimeZone, form.SiteTimeZone },
                { SetupConstants.DatabaseProvider, provider.Value },
                { SetupConstants.DatabaseConnectionString, connectionString },
                { SetupConstants.DatabaseTablePrefix, tablePrefix },
                { SetupConstants.DatabaseSchema, schema },
            },
#pragma warning restore CS8604
        };

        var executionId = await setupService.SetupAsync(context);
        return context.Errors.Count > 0
            ? new TenantResult(false, [.. context.Errors.Select(error => new TenantError(error.Key, error.Value))])
            : new TenantResult(true, [], settings, executionId);
    }

    private async Task<List<TenantError>> ValidateAsync(TenantForm form)
    {
        try
        {
            return [.. (await validator.ValidateAsync(form)).Select(error => new TenantError(error.Key, error.Message))];
        }
        catch (Exception exception) when (!exception.IsFatal())
        {
            logger.LogError(exception, "An error occurred while validating the tenant '{TenantName}'.", form.Name);
            return [new TenantError(string.Empty, S["An error occurred while validating the tenant database settings."])];
        }
    }

    // The host's configured database settings (the root configuration, not the current
    // tenant's) are presets every tenant takes: they replace what the form says.
    private (bool Provider, bool Configuration) ApplyPresets(TenantForm form, bool isUninitialized)
    {
        var presets = GetPresets();
        if (!presets.Provider || !isUninitialized)
        {
            return (presets.Provider, presets.Configuration);
        }

        form.DatabaseProvider = presets.DatabaseProvider;
        if (presets.Configuration)
        {
            form.ConnectionString = presets.ConnectionString;
            form.Schema = presets.Schema;
        }

        return (presets.Provider, presets.Configuration);
    }

    private (bool Provider, bool Configuration, string? DatabaseProvider, string? ConnectionString, string? Schema) GetPresets()
    {
        using var defaults = shellSettingsManager.CreateDefaultSettings().AsDisposable();
        var databaseProvider = defaults["DatabaseProvider"];
        if (string.IsNullOrEmpty(databaseProvider) || !_providers.TryGetValue(databaseProvider, out var provider))
        {
            return (false, false, null, null, null);
        }

        var connectionString = defaults["ConnectionString"];
        return (true, !provider.HasConnectionString || !string.IsNullOrWhiteSpace(connectionString), databaseProvider, connectionString, defaults["Schema"]);
    }

    private async Task<IReadOnlyList<TenantOption>> SetupRecipesAsync()
    {
        var collections = await Task.WhenAll(recipeHarvesters.Select(harvester => harvester.HarvestRecipesAsync()));
        return
        [
            .. collections.SelectMany(collection => collection)
                .Where(recipe => recipe.IsSetupRecipe)
                .OrderBy(recipe => recipe.DisplayName)
                .Select(recipe => new TenantOption(recipe.Name, recipe.DisplayName ?? recipe.Name)),
        ];
    }

    private async Task<IReadOnlyList<TenantOption>> FeatureProfilesAsync() =>
        services.GetService<IFeatureProfilesService>() is { } profiles
            ? [.. (await profiles.GetFeatureProfilesAsync()).Select(profile => new TenantOption(profile.Key, profile.Value.Name ?? profile.Key))]
            : [];

    private static string? Coalesce(string? first, string? second) => string.IsNullOrEmpty(first) ? second : first;
}
