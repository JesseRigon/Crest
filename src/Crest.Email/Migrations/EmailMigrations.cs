using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Crest.Data.Migration;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Scope;
using Crest.Settings;

namespace Crest.Email.Migrations;

public sealed class EmailMigrations : DataMigration
{
    private const string SmtpFeatureId = "Crest.Email.Smtp";

    public static int Create()
    {
        // In version 2.0, the Crest.Email.Smtp was split from Crest.Email. To ensure we keep the change
        // backward compatible, we added this migration step to auto-enable the new SMTP feature for sites that use the
        // Email service and have SmtpSettings.
        ShellScope.AddDeferredTask(async scope =>
        {
            var featuresManager = scope.ServiceProvider.GetRequiredService<IShellFeaturesManager>();

            if (await featuresManager.IsFeatureEnabledAsync(SmtpFeatureId))
            {
                return;
            }

            var siteService = scope.ServiceProvider.GetRequiredService<ISiteService>();
            var smtpSettings = await siteService.GetSettingsAsync<SmtpSettings>();

            if (!string.IsNullOrEmpty(smtpSettings.DefaultSender) ||
                scope.ServiceProvider.GetService<IOptions<SmtpOptions>>()?.Value.ConfigurationExists() == true)
            {
                // Enable the SMTP feature.
                await featuresManager.EnableFeaturesAsync(SmtpFeatureId);
            }
        });

        return 1;
    }
}
