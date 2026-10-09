using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Crest.DisplayManagement.Handlers;
using Crest.Modules;
using Crest.Navigation;
using Crest.Notifications;
using Crest.Security.Permissions;
using Crest.Sms.Activities;
using Crest.Sms.Drivers;
using Crest.Sms.Services;
using Crest.Workflows.Platform.Helpers;

namespace Crest.Sms;

public sealed class Startup : StartupBase
{
    private readonly IHostEnvironment _hostEnvironment;

    public Startup(IHostEnvironment hostEnvironment)
    {
        _hostEnvironment = hostEnvironment;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSmsServices();

        if (_hostEnvironment.IsDevelopment())
        {
            services.AddLogSmsProvider();
        }

        services.AddTwilioSmsProvider()
            .AddSiteDisplayDriver<TwilioSettingsDisplayDriver>();

        services.AddPermissionProvider<SmsPermissionProvider>();
        services.AddSiteDisplayDriver<SmsSettingsDisplayDriver>();
        services.AddNavigationProvider<AdminMenu>();
    }
}

[Feature("Crest.Notifications.Sms")]
public sealed class NotificationsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<INotificationMethodProvider, SmsNotificationProvider>();
    }
}

[RequireFeatures("Crest.Workflows")]
public sealed class WorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<SmsTask>();
    }
}
