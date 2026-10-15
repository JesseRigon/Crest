using Microsoft.Extensions.DependencyInjection;
using Grove.DisplayManagement.Handlers;
using Grove.Modules;
using Grove.Navigation;
using Grove.ReCaptcha.Configuration;
using Grove.ReCaptcha;
using Grove.ReCaptcha.Drivers;
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
        services.AddSiteDisplayDriver<ReCaptchaSettingsDisplayDriver>();
        services.AddPageSource<AdminPages>();
        services.AddPermissionProvider<Permissions>();
    }
}

[Feature("Grove.ReCaptcha")]
[RequireFeatures("Grove.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<ReCaptchaSettings, DeploymentStartup>(S => S["ReCaptcha settings"], S => S["Exports the ReCaptcha settings."]);
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
        services.AddDisplayDriver<LoginForm, ReCaptchaLoginFormDisplayDriver>();
    }
}

[Feature("Grove.ReCaptcha.Users")]
[RequireFeatures(UserConstants.Features.ResetPassword)]
public sealed class UsersResetPasswordStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<ForgotPasswordForm, ReCaptchaForgotPasswordFormDisplayDriver>();
        services.AddDisplayDriver<ResetPasswordForm, ReCaptchaResetPasswordFormDisplayDriver>();
    }
}

[Feature("Grove.ReCaptcha.Users")]
[RequireFeatures(UserConstants.Features.UserRegistration)]
public sealed class UsersRegistrationStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<RegisterUserForm, RegisterUserFormDisplayDriver>();
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
