using Grove.Display.Management.Document;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Grove.Data.Connectors;
using Grove.Data.Migration;
using Grove.Environment.Shell.Scope;
using Grove.Infrastructure.Settings;
using Grove.Integrations.Facebook.Connections;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Grove.Display.Management.Handlers;
using Grove.Integrations.Facebook.Filters;
using Grove.Integrations.Facebook.Services;
using Grove.Integrations.Facebook.Settings;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Security.Permissions;
using Grove.Integrations.Facebook.Workflows;
using Grove.Workflows;
using Grove.Workflows.Extensions;

namespace Grove.Integrations.Facebook;

[Feature(FacebookConstants.Features.Pixel)]
public sealed class StartupPixel : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSettingsGroup<FacebookPixelSettings>(
            FacebookConstants.PixelSettingsGroupId,
            "Meta Pixel",
            FacebookConstants.ManageFacebookPixelPermission,
            group => group.InMenu("settings-integrations", entryId: "facebookPixel").AlsoAdmits(Permissions.ManageFacebookApp));
        services.AddPermissionProvider<PixelPermissionProvider>();

        services.AddDocumentContributor<FacebookPixelDocumentContributor>();

        services.AddConversionsConnection();
        services.AddScoped<IMetaConversionsApiService, MetaConversionsApiService>();
        services.AddDataMigration<FacebookPixelSettingsMigrations>();
    }
}

[Feature(FacebookConstants.Features.Pixel)]
[RequireFeatures(WorkflowsConstants.FeatureId)]
public sealed class StartupPixelWorkflows : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IWorkflowActivityProvider, FacebookWorkflowProvider>();
        services.ConfigureGroveWorkflows(workflows => workflows.AddActivitiesFrom<StartupPixelWorkflows>());
    }
}


/// <summary>
/// Moves the Conversions API access token out of the pixel settings into the tenant credential
/// of the Meta Conversions API connection.
/// </summary>
internal sealed class FacebookPixelSettingsMigrations : DataMigration
{
    private const string TokenProperty = "ConversionsApiAccessToken";

    public static int Create()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var services = scope.ServiceProvider;
            var siteService = services.GetRequiredService<ISiteService>();
            var site = await siteService.LoadSiteSettingsAsync();

            if (site.Properties[nameof(FacebookPixelSettings)] is not JsonObject settings
                || settings[TokenProperty]?.GetValue<string>() is not { Length: > 0 } sealedToken)
            {
                return;
            }

            var logger = services.GetRequiredService<ILogger<FacebookPixelSettingsMigrations>>();

            try
            {
                var token = services.GetRequiredService<IDataProtectionProvider>().CreateProtector(FacebookConstants.ConversionsApiProtectorName).Unprotect(sealedToken);

                if (services.GetService<ICredentialStore>() is { } store)
                {
                    await store.SaveAsync(
                        FacebookConstants.ConversionsConnectionKey,
                        CredentialOwner.Tenant,
                        new ConnectionCredential { Values = new Dictionary<string, string> { [CredentialKeys.ApiKey] = token } },
                        label: "Dataset access token");
                }
                else
                {
                    logger.LogWarning("The Conversions API token could not be moved: the connector engine is not enabled. Enter it on the Connections screen.");
                }
            }
            catch (CryptographicException exception)
            {
                logger.LogWarning(exception, "The Conversions API token could not be opened; enter it again on the Connections screen.");
            }

            settings.Remove(TokenProperty);
            await siteService.UpdateSiteSettingsAsync(site);
        });

        return 1;
    }
}
