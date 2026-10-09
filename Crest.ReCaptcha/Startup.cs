using Microsoft.Extensions.DependencyInjection;
using Crest.DisplayManagement.Handlers;
using Crest.Modules;
using Crest.Navigation;
using Crest.ReCaptcha.Configuration;
using Crest.ReCaptcha.Core;
using Crest.ReCaptcha.Drivers;
using Crest.ReCaptcha.Users.Handlers;
using Crest.Security.Permissions;
using Crest.Settings.Deployment;
using Crest.Users;
using Crest.Users.Events;
using Crest.Users.Models;

namespace Crest.ReCaptcha;

[Feature("Crest.ReCaptcha")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddReCaptcha();
        services.AddSiteDisplayDriver<ReCaptchaSettingsDisplayDriver>();
        services.AddNavigationProvider<AdminMenu>();
        services.AddPermissionProvider<Permissions>();
    }
}

[Feature("Crest.ReCaptcha")]
[RequireFeatures("Crest.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<ReCaptchaSettings, DeploymentStartup>(S => S["ReCaptcha settings"], S => S["Exports the ReCaptcha settings."]);
    }
}

[Feature("Crest.ReCaptcha.Users")]
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

[Feature("Crest.ReCaptcha.Users")]
[RequireFeatures(UserConstants.Features.ResetPassword)]
public sealed class UsersResetPasswordStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<ForgotPasswordForm, ReCaptchaForgotPasswordFormDisplayDriver>();
        services.AddDisplayDriver<ResetPasswordForm, ReCaptchaResetPasswordFormDisplayDriver>();
    }
}

[Feature("Crest.ReCaptcha.Users")]
[RequireFeatures(UserConstants.Features.UserRegistration)]
public sealed class UsersRegistrationStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<RegisterUserForm, RegisterUserFormDisplayDriver>();
    }
}
