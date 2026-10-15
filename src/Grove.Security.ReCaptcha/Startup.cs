using Microsoft.Extensions.DependencyInjection;
using Grove.Environment.Options;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Security.ReCaptcha.Configuration;
using Grove.Security.ReCaptcha;
using Grove.Security.ReCaptcha.Users;
using Grove.Security.ReCaptcha.Users.Handlers;
using Grove.Security.Permissions;
using Grove.Infrastructure.Settings.Deployment;
using Grove.Access.Users;
using Grove.Access.Users.Events;
using Grove.Access.Users.Models;
using Grove.Workflows;
using Grove.Workflows.Extensions;
using Grove.Api;

namespace Grove.Security.ReCaptcha;

[Feature("Grove.Security.ReCaptcha")]
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
        services.AddApiContributor<Api.ReCaptchaApi>();
    }
}

[Feature("Grove.Security.ReCaptcha")]
[RequireFeatures("Grove.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<ReCaptchaSettings>();
    }
}

[Feature("Grove.Security.ReCaptcha.Users")]
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

[Feature("Grove.Security.ReCaptcha")]
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
