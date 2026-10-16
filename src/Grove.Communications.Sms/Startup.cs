using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Grove.Environment.Options;
using Grove.Modules;
using Grove.Security.Permissions;
using Grove.Communications.Sms.Workflows;
using Grove.Workflows;
using Grove.Workflows.Extensions;

namespace Grove.Communications.Sms;

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

        services.AddPermissionProvider<SmsPermissionProvider>();
        // The SMS test page (Grove.Communications.Sms.BlazorWasm) and its API.
        services.AddApiContributor<Api.SmsTestApi>();

        // Settings › SMS: the default provider; each provider adds its own section
        // (services.AddSettingsModel<T>(SmsSettings.GroupId, ...)).
        services.AddSettingsGroup<SmsSettings>(
            SmsSettings.GroupId,
            "SMS",
            SmsPermissions.ManageSmsSettings,
            group => group
                .InMenu("communications", position: "4", entryId: "sms")
                .OnSaved((services, _, _) =>
                {
                    services.GetRequiredService<IOptionsUpdateNotifier>().RequestUpdate<SmsProviderOptions>();
                    return Task.CompletedTask;
                }));
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
