using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Crest.Data.Migration;
using Crest.Entities;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Scope;
using Crest.Settings;
using Crest.Users.Models;

namespace Crest.Users.DataMigrations;

public sealed class ExternalAuthenticationMigrations : DataMigration
{
    public int Create()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var featuresManager = scope.ServiceProvider.GetRequiredService<IShellFeaturesManager>();

            var isRegistrationFeatureEnabled = await featuresManager.IsFeatureEnabledAsync(UserConstants.Features.UserRegistration);

            var siteService = scope.ServiceProvider.GetRequiredService<ISiteService>();

            var site = await siteService.LoadSiteSettingsAsync();

            var registrationSettings = site.Properties[nameof(RegistrationSettings)]?.AsObject() ?? new JsonObject();

            var enumValue = registrationSettings["UsersCanRegister"]?.GetValue<int>();

            site.Put(new ExternalRegistrationSettings
            {
                DisableNewRegistrations = enumValue == 0 || !isRegistrationFeatureEnabled,
                NoUsername = registrationSettings["NoUsernameForExternalUsers"]?.GetValue<bool>() ?? false,
                NoEmail = registrationSettings["NoEmailForExternalUsers"]?.GetValue<bool>() ?? false,
                NoPassword = registrationSettings["NoPasswordForExternalUsers"]?.GetValue<bool>() ?? false,
                GenerateUsernameScript = registrationSettings["GenerateUsernameScript"]?.ToString(),
                UseScriptToGenerateUsername = registrationSettings["UseScriptToGenerateUsername"]?.GetValue<bool>() ?? false,
            });

            var loginSettings = site.Properties[nameof(LoginSettings)]?.AsObject() ?? new JsonObject();

            site.Put(new ExternalLoginSettings
            {
                UseExternalProviderIfOnlyOneDefined = loginSettings["UseExternalProviderIfOnlyOneDefined"]?.GetValue<bool>() ?? false,
                UseScriptToSyncProperties = loginSettings["UseScriptToSyncRoles"]?.GetValue<bool>() ?? false,
                SyncPropertiesScript = loginSettings["SyncRolesScript"]?.ToString(),
            });

            await siteService.UpdateSiteSettingsAsync(site);

            if (enumValue is not null && enumValue != 1)
            {
                if (!isRegistrationFeatureEnabled)
                {
                    return;
                }

                await featuresManager.DisableFeaturesAsync(UserConstants.Features.UserRegistration);
            }
        });

        return 1;
    }
}
