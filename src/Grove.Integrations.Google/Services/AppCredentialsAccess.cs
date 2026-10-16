#nullable enable

using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Grove.Data.Connectors;
using Grove.Environment.Shell;
using Grove.Infrastructure.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grove.Integrations.Google.Services;

/// <summary>
/// Reads and writes the vendor's app credentials (tenant scope, sealed by the connector engine),
/// shared by the sign-in provider and the connection.
/// </summary>
internal static class AppCredentialsAccess
{
    /// <summary>
    /// Reads the app credentials in a scope of their own: authentication options are built
    /// outside any request scope. Null when none are stored or the engine is not enabled.
    /// </summary>
    public static AppCredentials? Read(IServiceProvider services, string key, ILogger logger)
    {
        try
        {
            using var scope = services.CreateScope();
            var provider = scope.ServiceProvider.GetService<IAppCredentialsProvider>();

            return provider?.GetAsync(key).GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The app credentials '{Key}' could not be read; enter them again on the Connections screen.", key);

            return null;
        }
    }

    /// <summary>
    /// Stores the app credentials (a recipe, a settings migration) as the tenant credential of the
    /// connection keyed like them, which the app credentials provider falls back to when the
    /// Connections screen has stored none of its own. A blank value keeps the stored one.
    /// </summary>
    public static async Task SaveAsync(IServiceProvider services, string key, string? clientId, string? clientSecret, CancellationToken cancellationToken = default)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            values[CredentialKeys.ClientId] = clientId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(clientSecret))
        {
            values[CredentialKeys.ClientSecret] = clientSecret;
        }

        if (values.Count == 0)
        {
            return;
        }

        var store = services.GetService<ICredentialStore>()
            ?? throw new InvalidOperationException($"App credentials '{key}' need the connector engine (feature '{ConnectorsConstants.FeatureId}').");

        await store.SaveAsync(key, CredentialOwner.Tenant, new ConnectionCredential { Values = values }, label: "App credentials", merge: true, cancellationToken);
    }

    /// <summary>
    /// Moves a client id and a sealed secret out of a site settings section into the app
    /// credentials, and removes them from the section (the secret was sealed with
    /// <paramref name="protectorPurpose"/>). A secret that no longer opens is dropped and logged.
    /// </summary>
    public static async Task MoveFromSiteSettingsAsync(
        IServiceProvider services,
        string section,
        string? clientIdProperty,
        string? secretProperty,
        string? protectorPurpose,
        string key,
        ILogger logger)
    {
        var siteService = services.GetRequiredService<ISiteService>();
        var site = await siteService.LoadSiteSettingsAsync();

        if (site.Properties[section] is not JsonObject settings)
        {
            return;
        }

        var clientId = clientIdProperty is null ? null : settings[clientIdProperty]?.GetValue<string>();
        var sealedSecret = secretProperty is null ? null : settings[secretProperty]?.GetValue<string>();
        string? secret = null;

        if (!string.IsNullOrWhiteSpace(sealedSecret) && protectorPurpose is not null)
        {
            try
            {
                secret = services.GetRequiredService<IDataProtectionProvider>().CreateProtector(protectorPurpose).Unprotect(sealedSecret);
            }
            catch (CryptographicException exception)
            {
                logger.LogWarning(exception, "The secret of '{Section}' could not be opened; enter the '{Key}' app credentials again.", section, key);
            }
        }

        if (services.GetService<ICredentialStore>() is not null)
        {
            await SaveAsync(services, key, clientId, secret);
        }
        else if (!string.IsNullOrWhiteSpace(clientId) || secret is not null)
        {
            logger.LogWarning("The '{Key}' app credentials could not be moved: the connector engine is not enabled. Enter them on the Connections screen.", key);
        }

        if (clientIdProperty is not null)
        {
            settings.Remove(clientIdProperty);
        }

        if (secretProperty is not null)
        {
            settings.Remove(secretProperty);
        }

        await siteService.UpdateSiteSettingsAsync(site);
    }
}

/// <summary>Reloads the tenant when the app credentials or the connection change, so the sign-in handler is rebuilt with them.</summary>
internal sealed class AppCredentialsChangeHandler(IShellReleaseManager shellReleaseManager, params string[] keys) : IConnectionChangeHandler
{
    public Task ConnectionChangedAsync(string key, ConnectionDefinition? definition, CancellationToken cancellationToken = default)
    {
        if (keys.Contains(key, StringComparer.OrdinalIgnoreCase))
        {
            shellReleaseManager.RequestRelease();
        }

        return Task.CompletedTask;
    }
}
