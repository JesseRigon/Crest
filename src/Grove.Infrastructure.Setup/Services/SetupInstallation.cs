#nullable enable

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Grove.Abstractions.Setup;
using Grove.Data;
using Grove.Communications.Email;
using Grove.Environment.Shell;
using Grove.Modules;
using Grove.Infrastructure.Recipes.Models;
using Grove.Infrastructure.Setup.Models;

namespace Grove.Infrastructure.Setup.Services;

/// <summary>What the installation page shows: the choices and what the tenant's settings preset.</summary>
public sealed class SetupChoices
{
    public IReadOnlyList<RecipeDescriptor> Recipes { get; init; } = [];

    public IReadOnlyList<DatabaseProvider> DatabaseProviders { get; init; } = [];

    public IEnumerable<KeyValuePair<string, string>> TimeZones { get; init; } = [];

    /// <summary>The tenant's settings name the database provider: the page does not ask.</summary>
    public DatabaseProvider? PresetDatabaseProvider { get; init; }

    /// <summary>The tenant's settings hold the whole database configuration: the page neither asks nor shows it.</summary>
    public bool DatabaseConfigurationPreset { get; init; }

    /// <summary>The tenant's settings name the recipe: the page does not ask.</summary>
    public bool RecipeNamePreset { get; init; }
}

/// <summary>
/// The Grove instance installation behind the static-SSR setup page: the choices, the token check
/// (a tenant created with a secret is set up only with a setup token from the Default tenant),
/// the validation and the call to <see cref="ISetupService"/>.
/// </summary>
public sealed class SetupInstallation(
    IClock clock,
    ISetupService setupService,
    ShellSettings shellSettings,
    IShellHost shellHost,
    IOptions<IdentityOptions> identityOptions,
    IEmailAddressValidator emailAddressValidator,
    IEnumerable<DatabaseProvider> databaseProviders,
    ITimeZoneSelectListProvider timeZones,
    IStringLocalizer<SetupInstallation> S,
    ILogger<SetupInstallation> logger)
{
    private readonly Dictionary<string, DatabaseProvider> _providers = databaseProviders.ToDictionary(provider => provider.Value, StringComparer.OrdinalIgnoreCase);

    public IdentityOptions IdentityOptions => identityOptions.Value;

    public async Task<SetupChoices> GetChoicesAsync()
    {
        // Recipes tagged "hidden" stay resolvable by name for programmatic setup but are not offered.
        var recipes = (await setupService.GetSetupRecipesAsync())
            .Where(recipe => recipe.Tags == null || !recipe.Tags.Contains("hidden", StringComparer.InvariantCultureIgnoreCase))
            .OrderBy(recipe => recipe.DisplayName ?? recipe.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        return new SetupChoices
        {
            Recipes = recipes,
            DatabaseProviders = [.. _providers.Values],
            TimeZones = await timeZones.GetTimeZoneSelectListAsync(),
            PresetDatabaseProvider = PresetDatabaseProvider(),
            DatabaseConfigurationPreset = HasPresetDatabaseConfiguration(),
            RecipeNamePreset = !string.IsNullOrEmpty(shellSettings["RecipeName"]),
        };
    }

    /// <summary>The form's first values: the default recipe, the system time zone, the preset database settings.</summary>
    public async Task<SetupForm> NewFormAsync(string? token)
    {
        var choices = await GetChoicesAsync();
        var defaultRecipe = choices.Recipes.FirstOrDefault(recipe => recipe.Tags?.Contains("default") == true) ?? choices.Recipes.FirstOrDefault();

        return new SetupForm
        {
            RecipeName = choices.RecipeNamePreset ? shellSettings["RecipeName"] : defaultRecipe?.Name,
            SiteTimeZone = clock.GetSystemTimeZone().TimeZoneId,
            DatabaseProvider = choices.PresetDatabaseProvider?.Value ?? choices.DatabaseProviders.FirstOrDefault(provider => provider.IsDefault)?.Value,
            TablePrefix = shellSettings["TablePrefix"],
            Schema = shellSettings["Schema"],
            Secret = token,
        };
    }

    /// <summary>Whether this request may set the tenant up: a tenant with a secret needs a valid, unexpired setup token.</summary>
    public async Task<bool> MayProceedAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(shellSettings["Secret"]))
        {
            return true;
        }

        if (string.IsNullOrEmpty(token) || !await IsTokenValidAsync(token))
        {
            logger.LogWarning("An attempt to access '{TenantName}' without providing a secret was made", shellSettings.Name);
            return false;
        }

        return true;
    }

    /// <summary>Validates the form and runs the setup; the errors by field (<c>""</c> for the form), empty when the tenant is set up.</summary>
    public async Task<IReadOnlyDictionary<string, string>> InstallAsync(SetupForm form)
    {
        var errors = new Dictionary<string, string>(StringComparer.Ordinal);
        var recipes = await setupService.GetSetupRecipesAsync();

        if (string.IsNullOrEmpty(form.Password))
        {
            errors[nameof(SetupForm.Password)] = S["The password is required."];
        }

        if (form.Password != form.PasswordConfirmation)
        {
            errors[nameof(SetupForm.PasswordConfirmation)] = S["The password confirmation doesn't match the password."];
        }

        var recipeName = !string.IsNullOrEmpty(shellSettings["RecipeName"]) ? shellSettings["RecipeName"] : form.RecipeName;
        var recipe = string.IsNullOrEmpty(recipeName) ? null : recipes.FirstOrDefault(candidate => candidate.Name == recipeName);
        if (recipe is null)
        {
            errors[nameof(SetupForm.RecipeName)] = S["Invalid recipe."];
        }

        if (!string.IsNullOrEmpty(form.Email) && !emailAddressValidator.Validate(form.Email))
        {
            errors[nameof(SetupForm.Email)] = S["The email is invalid."];
        }

        if (!string.IsNullOrEmpty(form.UserName) && form.UserName.Any(character => !IdentityOptions.User.AllowedUserNameCharacters.Contains(character)))
        {
            errors[nameof(SetupForm.UserName)] = S["User name '{0}' is invalid, can only contain letters or digits.", form.UserName];
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        // The setup properties are nullable by contract (an empty table prefix, no schema).
#pragma warning disable CS8604
        var context = new SetupContext
        {
            ShellSettings = shellSettings,
            EnabledFeatures = null,
            Errors = new Dictionary<string, string>(),
            Recipe = recipe,
            Properties = new Dictionary<string, object>
            {
                { SetupConstants.SiteName, form.SiteName },
                { SetupConstants.AdminUsername, form.UserName },
                { SetupConstants.AdminEmail, form.Email },
                { SetupConstants.AdminPassword, form.Password },
                { SetupConstants.SiteTimeZone, form.SiteTimeZone },
                { SetupConstants.DatabaseProvider, PresetDatabaseProvider()?.Value ?? form.DatabaseProvider },
            },
        };
#pragma warning restore CS8604

        if (HasPresetDatabaseConfiguration())
        {
            context.Properties[SetupConstants.DatabaseConnectionString] = shellSettings["ConnectionString"];
            context.Properties[SetupConstants.DatabaseTablePrefix] = shellSettings["TablePrefix"];
            context.Properties[SetupConstants.DatabaseSchema] = shellSettings["Schema"];
        }
        else
        {
            context.Properties[SetupConstants.DatabaseConnectionString] = form.ConnectionString;
            context.Properties[SetupConstants.DatabaseTablePrefix] = form.TablePrefix;
            context.Properties[SetupConstants.DatabaseSchema] = form.Schema;
        }

        try
        {
            await setupService.SetupAsync(context);
        }
        catch (Exception exception) when (!exception.IsFatal())
        {
            logger.LogError(exception, "An error occurred while setting up the tenant '{TenantName}'.", shellSettings.Name);
            errors[string.Empty] = S["An error occurred while running the setup. Try again in a few minutes, and if the issue persists, consult the app's operator."];
            return errors;
        }

        // A setup component may refuse (the database connection, for instance).
        foreach (var error in context.Errors)
        {
            errors[error.Key] = error.Value;
        }

        return errors;
    }

    private bool HasPresetDatabaseConfiguration() =>
        PresetDatabaseProvider() is { } provider && (!provider.HasConnectionString || !string.IsNullOrWhiteSpace(shellSettings["ConnectionString"]));

    private DatabaseProvider? PresetDatabaseProvider()
    {
        var name = shellSettings["DatabaseProvider"];
        return string.IsNullOrEmpty(name) ? null : _providers.GetValueOrDefault(name);
    }

    private async Task<bool> IsTokenValidAsync(string token)
    {
        var result = false;
        try
        {
            var shellScope = await shellHost.GetScopeAsync(ShellSettings.DefaultShellName);
            await shellScope.UsingAsync(scope =>
            {
                var protector = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector("Tokens").ToTimeLimitedDataProtector();
                var value = protector.Unprotect(token, out var expiration);
                result = clock.UtcNow < expiration.ToUniversalTime() && shellSettings["Secret"] == value;
                return Task.CompletedTask;
            });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error in decrypting the token");
        }

        return result;
    }
}
