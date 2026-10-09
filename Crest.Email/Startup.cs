using Microsoft.Extensions.DependencyInjection;
using Crest.Data.Migration;
using Crest.DisplayManagement.Handlers;
using Crest.Email.Drivers;
using Crest.Email.Migrations;
using Crest.Email.Services;
using Crest.Modules;
using Crest.Navigation;
using Crest.Security.Permissions;

namespace Crest.Email;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddEmailServices()
            .AddSignalOptionsChangeTokenSource<EmailOptions>()
            .AddSignalOptionsChangeTokenSource<EmailProviderOptions>()
            .AddSiteDisplayDriver<EmailSettingsDisplayDriver>()
            .AddSiteSettingsPermission(EmailSettings.GroupId, EmailPermissions.ManageEmailSettings)
            .AddPermissionProvider<Permissions>()
            .AddNavigationProvider<AdminMenu>();

        services.AddDataMigration<EmailMigrations>();
    }
}
