using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Grove.DisplayManagement.Handlers;
using Grove.Modules;
using Grove.Navigation;
using Grove.Notifications;
using Grove.Security.Permissions;
using Grove.Sms.Drivers;
using Grove.Sms.Services;
using Grove.Sms.Workflows;
using Grove.Workflows;
using Grove.Workflows.Extensions;

namespace Grove.Sms;

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
        services.AddPageSource<AdminPages>();
    }
}

[Feature("Grove.Notifications.Sms")]
public sealed class NotificationsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<INotificationMethodProvider, SmsNotificationProvider>();
    }
}

[RequireFeatures(WorkflowsConstants.FeatureId)]
public sealed class WorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IWorkflowActivityProvider, SmsWorkflowProvider>();
        services.ConfigureGroveWorkflows(workflows => workflows.AddActivitiesFrom<WorkflowsStartup>());
    }
}
