using Microsoft.Extensions.DependencyInjection;
using Grove.Data.Migration;
using Grove.DisplayManagement.Handlers;
using Grove.Email.Migrations;
using Grove.Email.Services;
using Grove.Modules;
using Grove.Navigation;
using Grove.Security.Permissions;

namespace Grove.Email;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddEmailServices()
            .AddSignalOptionsChangeTokenSource<EmailOptions>()
            .AddSignalOptionsChangeTokenSource<EmailProviderOptions>()
            .AddSiteSettingsPermission(EmailSettings.GroupId, EmailPermissions.ManageEmailSettings)
            .AddPermissionProvider<Permissions>();

        services.AddDataMigration<EmailMigrations>();
    }
}
