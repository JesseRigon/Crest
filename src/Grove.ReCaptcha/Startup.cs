using Microsoft.Extensions.DependencyInjection;
using Grove.Environment.Options;
using Grove.Modules;
using Grove.Navigation;
using Grove.ReCaptcha.Configuration;
using Grove.ReCaptcha;
using Grove.ReCaptcha.Users;
using Grove.ReCaptcha.Users.Handlers;
using Grove.Security.Permissions;
using Grove.Settings.Deployment;
using Grove.Users;
using Grove.Users.Events;
using Grove.Users.Models;
using Grove.Workflows;
using Grove.Workflows.Extensions;

namespace Grove.ReCaptcha;

[Feature("Grove.ReCaptcha")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddReCaptcha();
        services.AddSettingsGroup<ReCaptchaSettings>(
            ReCaptchaSettings.GroupId,
            "reCaptcha",
            ReCaptchaPermissions.ManageReCaptchaSettings,
            group => group
                .InMenu("settings-security", entryId: "recaptcha")
                .OnSaved((services, _, _) =>
                {
                    services.GetRequiredService<IOptionsUpdateNotifier>().RequestUpdate<ReCaptchaSettings>();
                    return Task.CompletedTask;
                }));
        services.AddPermissionProvider<Permissions>();
    }
}

[Feature("Grove.ReCaptcha")]
[RequireFeatures("Grove.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<ReCaptchaSettings>();
    }
}

[Feature("Grove.ReCaptcha.Users")]
public sealed class UsersStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRegistrationFormEvents, RegistrationFormEventHandler>();
        services.AddScoped<ILoginFormEvent, LoginFormEventEventHandler>();
        services.AddScoped<IPasswordRecoveryFormEvents, PasswordRecoveryFormEventEventHandler>();

        // The captcha the sign-in, registration and password recovery forms render.
        services.AddScoped<Grove.Access.ILoginExtensionProvider, ReCaptchaLoginExtensions>();
    }
}

[Feature("Grove.ReCaptcha")]
[RequireFeatures(Grove.Workflows.WorkflowsConstants.FeatureId)]
public sealed class WorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // The form captcha's check (docs/widgets.md): the token verified by a workflow activity.
        services.AddScoped<IWorkflowActivityProvider, Workflows.ReCaptchaWorkflowProvider>();
        services.ConfigureGroveWorkflows(workflows => workflows.AddActivitiesFrom<WorkflowsStartup>());
    }
}
