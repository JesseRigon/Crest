using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Grove.Display.Management.Handlers;
using Grove.Environment.Options;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Communications;
using Grove.Security.Permissions;
using Grove.Communications.Sms.Models;
using Grove.Communications.Sms.Services;
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

        services.AddTwilioSmsProvider();

        services.AddPermissionProvider<SmsPermissionProvider>();
        // The SMS test page (Grove.Communications.Sms.BlazorWasm) and its API.
        services.AddApiContributor<Api.SmsTestApi>();
        services.AddSettingsGroup<SmsSettings>(
            SmsSettings.GroupId,
            "SMS",
            SmsPermissions.ManageSmsSettings,
            group => group.InMenu("settings-communication", entryId: "sms"));
        services.AddSettingsModel<TwilioSettings>(SmsSettings.GroupId, "Twilio", model => model
            .ValidatedBy((services, settings, errors, _) =>
            {
                if (settings.IsEnabled)
                {
                    SmsProviderSettings.ValidatePhoneNumber(services, settings.PhoneNumber, nameof(TwilioSettings.PhoneNumber), errors);

                    if (string.IsNullOrWhiteSpace(settings.AccountSID))
                    {
                        errors.Add(nameof(TwilioSettings.AccountSID), "Account SID requires a value.");
                    }

                    if (string.IsNullOrWhiteSpace(settings.AuthToken))
                    {
                        errors.Add(nameof(TwilioSettings.AuthToken), "Auth Token requires a value.");
                    }
                }

                return Task.CompletedTask;
            })
            .OnSaved(async (services, settings, _) =>
            {
                await SmsProviderSettings.UpdateDefaultProviderAsync(services, TwilioSmsProvider.TechnicalName, settings.IsEnabled);
                services.GetRequiredService<IOptionsUpdateNotifier>()
                    .RequestUpdate<TwilioOptions>()
                    .RequestUpdate<SmsProviderOptions>();
            }));
    }
}

[Feature("Grove.Communications.Notifications.Sms")]
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
