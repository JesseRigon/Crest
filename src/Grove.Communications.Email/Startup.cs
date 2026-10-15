using Microsoft.Extensions.DependencyInjection;
using Grove.Data.Migration;
using Grove.Display.Management.Handlers;
using Grove.Communications.Email.Migrations;
using Grove.Communications.Email.Services;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Security.Permissions;

namespace Grove.Communications.Email;

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
        services.AddApiContributor<Api.EmailSettingsApi>();
    }
}
