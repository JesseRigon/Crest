using Microsoft.Extensions.DependencyInjection;
using Grove.Communications.Email.Services;
using Grove.Modules;
using Grove.Security.Permissions;

namespace Grove.Communications.Email;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddEmailServices()
            .AddSignalOptionsChangeTokenSource<EmailOptions>()
            .AddSignalOptionsChangeTokenSource<EmailProviderOptions>()
            .AddSignalOptionsChangeTokenSource<EmailReceiveProviderOptions>()
            .AddPermissionProvider<Permissions>();

        // Settings › Email: the default send and receive providers; each provider adds its own
        // section (services.AddSettingsModel<T>(EmailSettings.GroupId, ...)).
        services.AddSettingsGroup<EmailSettings>(
            EmailSettings.GroupId,
            "Email",
            EmailPermissions.ManageEmailSettings,
            group => group
                .InMenu("communications", position: "2", entryId: "email")
                .OnSaved((services, _, _) =>
                {
                    services.GetRequiredService<Grove.Environment.Options.IOptionsUpdateNotifier>().RequestUpdate<EmailOptions>();
                    return Task.CompletedTask;
                }));

        services.AddApiContributor<Api.EmailTestApi>();
    }
}
