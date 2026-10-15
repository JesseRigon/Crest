using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Data.Migration;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Scope;
using Grove.Settings;

namespace Grove.Email.Migrations;

public sealed class EmailMigrations : DataMigration
{
    private const string SmtpFeatureId = "Grove.Email.Smtp";

    public static int Create()
    {
        // In version 2.0, the Grove.Email.Smtp was split from Grove.Email. To ensure we keep the change
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
