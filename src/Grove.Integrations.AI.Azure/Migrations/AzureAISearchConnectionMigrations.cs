#nullable enable

using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Grove.Data.Connectors;
using Grove.Data.Migration;
using Grove.Environment.Shell.Scope;
using Grove.Infrastructure.Settings;
using Grove.Integrations.AI.Azure.Models;
using Grove.Integrations.AI.Azure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grove.Integrations.AI.Azure;

/// <summary>
/// Moves a custom Azure AI Search configuration out of the settings into a defined SDK
/// connection (<see cref="AzureAISearchDefaultSettings.DefaultConnection"/>): the endpoint as its
/// base URL, the API key as its tenant credential, the managed identity as its settings.
/// </summary>
internal sealed class AzureAISearchConnectionMigrations : DataMigration
{
    private const string LegacyProtectorName = "AzureAISearch";

    public static int Create()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILogger<AzureAISearchConnectionMigrations>>();
            var siteService = services.GetRequiredService<ISiteService>();
            var site = await siteService.LoadSiteSettingsAsync();

            if (site.Properties[nameof(AzureAISearchDefaultSettings)] is not JsonObject settings || settings.ContainsKey(nameof(AzureAISearchDefaultSettings.Connection)))
            {
                return;
            }

            var useCustom = settings["UseCustomConfiguration"]?.GetValue<bool>() == true;
            var endpoint = settings["Endpoint"]?.GetValue<string>();

            if (useCustom && !string.IsNullOrWhiteSpace(endpoint))
            {
                if (services.GetService<IDefinedConnectionStore>() is { } connections && services.GetService<ICredentialStore>() is { } credentials)
                {
                    string? apiKey = null;
                    if (settings["ApiKey"]?.GetValue<string>() is { Length: > 0 } sealedKey)
                    {
                        try
                        {
                            apiKey = services.GetRequiredService<IDataProtectionProvider>().CreateProtector(LegacyProtectorName).Unprotect(sealedKey);
                        }
                        catch (CryptographicException exception)
                        {
                            logger.LogWarning(exception, "The Azure AI Search API key could not be opened; enter it again on the Connections screen.");
                        }
                    }

                    var connectionSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (settings["IdentityClientId"]?.GetValue<string>() is { Length: > 0 } identityClientId)
                    {
                        connectionSettings[AzureAISearchDefaultOptionsConfigurations.IdentityClientIdSetting] = identityClientId;
                    }

                    if (settings["AuthenticationType"]?.ToString() is "2" or "ManagedIdentity")
                    {
                        connectionSettings[AzureAISearchDefaultOptionsConfigurations.AuthenticationSetting] = nameof(AzureAIAuthenticationType.ManagedIdentity);
                    }

                    await connections.SaveAsync(new ConnectionDefinition
                    {
                        Key = AzureAISearchDefaultSettings.DefaultConnection,
                        DisplayName = "Azure AI Search",
                        Protocol = ConnectorProtocols.Sdk,
                        Origin = ConnectionOrigin.Defined,
                        BaseUrl = endpoint.Trim(),
                        Auth = apiKey is null ? ConnectionAuth.None : new ConnectionAuth { Scheme = AuthSchemes.ApiKey, KeyName = "api-key" },
                        Settings = connectionSettings,
                    });

                    if (apiKey is not null)
                    {
                        await credentials.SaveAsync(
                            AzureAISearchDefaultSettings.DefaultConnection,
                            CredentialOwner.Tenant,
                            new ConnectionCredential { Values = new Dictionary<string, string> { [CredentialKeys.ApiKey] = apiKey } },
                            label: "API key");
                    }
                }
                else
                {
                    logger.LogWarning("The Azure AI Search configuration could not be moved: the connector engine is not enabled. Define the '{Key}' connection on the Connections screen.", AzureAISearchDefaultSettings.DefaultConnection);
                }
            }

            site.Properties[nameof(AzureAISearchDefaultSettings)] = new JsonObject
            {
                [nameof(AzureAISearchDefaultSettings.Connection)] = AzureAISearchDefaultSettings.DefaultConnection,
            };
            await siteService.UpdateSiteSettingsAsync(site);
        });

        return 1;
    }
}
