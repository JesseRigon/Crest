using System.Security.Claims;
using System.Web;
using Fluid;
using Fluid.Values;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Grove.Infrastructure.Shells.Admin.Models;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata.Models;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Deployment;
using Grove.Display.Management;
using Grove.Display.Management.Handlers;
using Grove.Display.Management.Theming;
using Grove.Environment.Commands;
using Grove.Environment.Options;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Configuration;
using Grove.Environment.Shell.Scope;
using Grove.Display.Liquid;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Security.RateLimits;
using Grove.Infrastructure.Recipes;
using Grove.Infrastructure.Recipes.Services;
using Grove.Security;
using Grove.Security.Permissions;
using Grove.Infrastructure.Settings.Deployment;
using Grove.Infrastructure.Setup.Events;
using Grove.Access.Users.Commands;
using Grove.Access.Users.Services;
using Grove.Access.Users.DataMigrations;
using Grove.Access.Users.Deployment;
using Grove.Access.Users.Events;
using Grove.Access.Users.Handlers;
using Grove.Access.Users.Indexes;
using Grove.Access.Users.Liquid;
using Grove.Access.Users.Models;
using Grove.Access.Users.Recipes;
using Grove.Access.Users.Services;
using Grove.Access.Users.ViewModels;
using YesSql.Filters.Query;

namespace Grove.Access.Users;

public sealed class Startup : StartupBase
{
    private readonly string _tenantName;

    private UserOptions _userOptions;

    public Startup(ShellSettings shellSettings)
    {
        _tenantName = shellSettings.Name;
    }

    public override void ConfigureServices(IServiceCollection services)
    {

        // The sign-in, users and login settings APIs (docs/access.md: Access declares them).
        services.AddScoped<LoginService>();
        services.AddApiContributor<Api.UsersApi>();
        services.AddApiContributor<Api.AuthApi>();
        services.AddApiContributor<Api.LoginSettingsApi>();
        services.AddApiContributor<Api.UserCultureApi>();
        services.AddApiContributor<Api.AccountApi>();
        services.AddApiContributor<Api.UserSettingsApi>();


        services.AddDataMigration<ExternalAuthenticationMigrations>();

        services.Configure<UserOptions>(userOptions =>
        {
            var configuration = ShellScope.Services.GetRequiredService<IShellConfiguration>();
            configuration.GetSection("Grove_Users").Bind(userOptions);
        });

        // Add ILookupNormalizer as Singleton because it is needed by UserIndexProvider
        services.TryAddSingleton<ILookupNormalizer, UpperInvariantLookupNormalizer>();

        // Add the default token providers used to generate tokens for an admin to change user's password.
        services.AddIdentity<IUser, IRole>()
            .AddTokenProvider<DataProtectorTokenProvider<IUser>>(TokenOptions.DefaultProvider);

        // Configure token provider for email confirmation.
        services.AddTransient<IConfigureOptions<IdentityOptions>, EmailConfirmationIdentityOptionsConfigurations>()
            .AddTransient<EmailConfirmationTokenProvider>()
            .AddOptions<EmailConfirmationTokenProviderOptions>();

        services.AddTransient<IConfigureOptions<IdentityOptions>, IdentityOptionsConfigurations>();
        // Configure the authentication options to use the application cookie scheme as the default sign-out handler.
        // This is required for security modules like the OpenID module (that uses SignOutAsync()) to work correctly.
        services.AddAuthentication(options => options.DefaultSignOutScheme = IdentityConstants.ApplicationScheme);

        services.AddUsers();

        services.ConfigureApplicationCookie(options =>
        {
            var userOptions = ShellScope.Services.GetRequiredService<IOptions<UserOptions>>();

            options.Cookie.Name = "orchauth_" + HttpUtility.UrlEncode(_tenantName);

            // Don't set the cookie builder 'Path' so that it uses the 'IAuthenticationFeature' value
            // set by the pipeline and coming from the request 'PathBase' which already ends with the
            // tenant prefix but may also start by a path related e.g to a virtual folder.

            options.LoginPath = "/" + userOptions.Value.LoginPath;
            // Signing out is the sign-in API's (AuthApi); the cookie handler treats a sign-out there as its own.
            options.LogoutPath = Api.AuthApi.LogoutPath;
            options.AccessDeniedPath = "/Error/403";
        });

        services.AddTransient<IPostConfigureOptions<SecurityStampValidatorOptions>, ConfigureSecurityStampOptions>();
        services.AddDataMigration<Migrations>();

        services.AddScoped<IUserClaimsProvider, EmailClaimsProvider>();
        services.AddSingleton<IUserIdGenerator, DefaultUserIdGenerator>();

        services.AddScoped<IMembershipService, MembershipService>();
        services.AddScoped<ISetupEventHandler, SetupEventHandler>();
        services.AddScoped<ICommandHandler, UserCommands>();
        services.AddScoped<IExternalLoginEventHandler, ScriptExternalLoginEventHandler>();

        services.AddPermissionProvider<Permissions>();


        services.AddScoped<IRecipeEnvironmentProvider, RecipeEnvironmentSuperUserProvider>();



        services.AddResourceConfiguration<UserOptionsConfiguration>();

        services.AddRecipeExecutionStep<UsersStep>();

        services.AddScoped<CustomUserSettingsService>();
        services.AddRecipeExecutionStep<CustomUserSettingsStep>();
        services.AddScoped<ILoginFormEvent, EmailConfirmationLoginFormEvent>();
        services.AddScoped<ILoginFormEvent, DisabledUserLoginFormEvent>();
    }

    public override void Configure(IApplicationBuilder builder, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        _userOptions ??= serviceProvider.GetRequiredService<IOptions<UserOptions>>().Value;

        builder.UseAuthorization();
    }

}

[RequireFeatures("Grove.Security.RateLimits")]
public sealed class RateLimitsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<RateLimitsOptions>(options =>
            options.AddRouteRateLimit(RateLimitRouteNames.Login, HttpMethods.Post, RateLimitPartitionHelpers.CreateSlidingWindowPerIpPolicy(UserRateLimiterPolicyNames.PasswordAuthentication, 10)));
    }
}

[Feature(UserConstants.Features.ExternalAuthentication)]
public sealed class ExternalAuthenticationStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ILoginFormEvent, ExternalLoginFormEvents>();
        services.AddApiContributor<Api.ExternalAuthenticationApi>();
        services.AddApiContributor<Api.ExternalLoginApi>();

        // The sign-in form's contributions: a button per external identity provider.
        services.AddScoped<Grove.Access.ILoginExtensionProvider, ExternalProviderLoginExtensions>();
        services.AddSignalOptionsChangeTokenSource<ExternalLoginOptions>();
        services.AddTransient<IConfigureOptions<ExternalLoginOptions>, ExternalLoginOptionsConfigurations>();
    }
}

[Feature(UserConstants.Features.ExternalAuthentication)]
[RequireFeatures("Grove.Deployment")]
public sealed class ExternalAuthenticationDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<ExternalRegistrationSettings>();
        services.AddSiteSettingsPropertyDeploymentStep<ExternalLoginSettings>();
    }
}

[Feature(UserConstants.Features.ExternalAuthentication)]
[RequireFeatures("Grove.Security.RateLimits")]
public sealed class ExternalAuthenticationRateLimitsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<RateLimitsOptions>(options =>
        {
            options.AddRouteRateLimit(RateLimitRouteNames.RegisterExternalLogin, HttpMethods.Post, RateLimitPartitionHelpers.CreateSlidingWindowPerIpPolicy(UserRateLimiterPolicyNames.UserRegistration, 3));
            options.AddRouteRateLimit(RateLimitRouteNames.LinkExternalLogin, HttpMethods.Post, RateLimitPartitionHelpers.CreateSlidingWindowPerIpPolicy(UserRateLimiterPolicyNames.PasswordAuthentication, 10));
        });
    }
}

[RequireFeatures("Grove.Communications.Email")]
public sealed class EmailStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<UserEmailService>();
        services.AddScoped<IRegistrationFormEvents, EmailConfirmationRegistrationFormEvents>();
    }
}

[RequireFeatures("Grove.Access.Roles")]
public sealed class RolesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRoleRemovedEventHandler, UserRoleRemovedEventHandler>();
        services.AddScoped<IRoleUpdatedEventHandler, UserRoleUpdatedEventHandler>();
        services.AddIndexProvider<UserByRoleNameIndexProvider>();
        services.AddScoped<Grove.Access.IResourcePermissionMapper, RolePermissionMapper>();
        services.AddPermissionProvider<UserRolePermissions>();
    }
}

[RequireFeatures("Grove.Display.Liquid")]
public sealed class LiquidStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.Filters.AddFilter("has_claim", UserFilters.HasClaim);
            o.Filters.AddFilter("user_id", UserFilters.UserId);

            o.MemberAccessStrategy.Register<ClaimsPrincipal>();
            o.MemberAccessStrategy.Register<ClaimsIdentity>();

            o.Scope.SetValue("User", new ObjectValue(new LiquidUserAccessor()));
            o.MemberAccessStrategy.Register<LiquidUserAccessor, FluidValue>((obj, name, ctx) =>
            {
                var user = ((LiquidTemplateContext)ctx).Services.GetRequiredService<IHttpContextAccessor>().HttpContext?.User;
                if (user != null)
                {
                    return name switch
                    {
                        nameof(ClaimsPrincipal.Identity) => new ObjectValue(user.Identity),
                        _ => NilValue.Instance
                    };
                }

                return NilValue.Instance;
            });

            o.MemberAccessStrategy.Register<User, FluidValue>((user, name, context) =>
            {
                return name switch
                {
                    nameof(User.UserId) => new StringValue(user.UserId),
                    nameof(User.UserName) => new StringValue(user.UserName),
                    nameof(User.NormalizedUserName) => new StringValue(user.NormalizedUserName),
                    nameof(User.Email) => new StringValue(user.Email),
                    nameof(User.NormalizedEmail) => new StringValue(user.NormalizedEmail),
                    nameof(User.EmailConfirmed) => user.EmailConfirmed ? BooleanValue.True : BooleanValue.False,
                    nameof(User.IsEnabled) => user.IsEnabled ? BooleanValue.True : BooleanValue.False,
                    nameof(User.RoleNames) => new ArrayValue(user.RoleNames.Select(x => new StringValue(x)).ToArray()),
                    nameof(User.Properties) => new ObjectValue(user.Properties),
                    _ => NilValue.Instance
                };
            });
        })
       .AddLiquidFilter<UsersByIdFilter>("users_by_id")
       .AddLiquidFilter<UsersByNameFilter>("users_by_name")
       .AddLiquidFilter<HasPermissionFilter>("has_permission")
       .AddLiquidFilter<IsInRoleFilter>("is_in_role")
       .AddLiquidFilter<UserEmailFilter>("user_email");
    }
}

[RequireFeatures("Grove.Deployment")]
public sealed class LoginDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<LoginSettings>();
    }
}

[Feature("Grove.Access.Users.ChangeEmail")]
public sealed class ChangeEmailStartup : StartupBase
{
    // The change-email page is the account components' (step 7: the MVC ChangeEmail screens are
    // retired); this feature keeps its settings and identity options.
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<IdentityOptions>, ChangeEmailIdentityOptionsConfigurations>()
            .AddTransient<ChangeEmailTokenProvider>()
            .AddOptions<ChangeEmailTokenProviderOptions>();

        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<ChangeEmailViewModel>();
        });
    }
}

[Feature("Grove.Access.Users.ChangeEmail")]
[RequireFeatures("Grove.Deployment")]
public sealed class ChangeEmailDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<ChangeEmailDeploymentStartup>();
    }
}

[Feature(UserConstants.Features.UserRegistration)]
public sealed class RegistrationStartup : StartupBase
{
    // The registration pages are the site shell's (Grove.Access.Users' site library): the API and the
    // sign-in form's "register" link are declared here.
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<ConfirmEmailViewModel>();
        });

        services.AddSignalOptionsChangeTokenSource<RegistrationOptions>();

        services.AddApiContributor<Api.RegistrationApi>();
        services.AddScoped<Grove.Access.ILoginExtensionProvider, RegistrationLoginExtensions>();
        services.AddTransient<IConfigureOptions<RegistrationOptions>, RegistrationOptionsConfigurations>();
        services.AddScoped<ILoginFormEvent, UserModerationLoginFormEvent>();
        services.AddScoped<IRegistrationFormEvents, UserModerationRegistrationFormEvents>();
    }
}

[Feature(UserConstants.Features.UserRegistration)]
[RequireFeatures("Grove.Security.RateLimits")]
public sealed class RegistrationRateLimitsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<RateLimitsOptions>(options =>
            options.AddRouteRateLimit(RateLimitRouteNames.Register, HttpMethods.Post, RateLimitPartitionHelpers.CreateSlidingWindowPerIpPolicy(UserRateLimiterPolicyNames.UserRegistration, 3)));
    }
}

[Feature(UserConstants.Features.UserRegistration)]
[RequireFeatures("Grove.Deployment")]
public sealed class RegistrationDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<RegistrationSettings>();
    }
}

[Feature(UserConstants.Features.ResetPassword)]
public sealed class ResetPasswordStartup : StartupBase
{
    // The forgot and reset password pages are the site shell's (Grove.Access.Users' site library): the
    // API and the sign-in form's "forgot your password" link are declared here.
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<IdentityOptions>, PasswordResetIdentityOptionsConfigurations>()
            .AddTransient<PasswordResetTokenProvider>()
            .AddOptions<PasswordResetTokenProviderOptions>();

        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<LostPasswordViewModel>();
        });

        services.AddApiContributor<Api.PasswordResetApi>();
        services.AddScoped<Grove.Access.ILoginExtensionProvider, ResetPasswordLoginExtensions>();
    }
}

[Feature(UserConstants.Features.ResetPassword)]
[RequireFeatures("Grove.Security.RateLimits")]
public sealed class ResetPasswordRateLimitsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<RateLimitsOptions>(options =>
        {
            options.AddRouteRateLimit(RateLimitRouteNames.ForgotPassword, HttpMethods.Post, RateLimitPartitionHelpers.CreateSlidingWindowPerIpPolicy(UserRateLimiterPolicyNames.PasswordRecovery, 5));
            options.AddRouteRateLimit(RateLimitRouteNames.ResetPassword, HttpMethods.Post, RateLimitPartitionHelpers.CreateSlidingWindowPerIpPolicy(UserRateLimiterPolicyNames.PasswordRecovery, 5));
        });
    }
}

[Feature(UserConstants.Features.ResetPassword)]
[RequireFeatures("Grove.Deployment")]
public sealed class ResetPasswordDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<ResetPasswordSettings>();
    }
}

[Feature("Grove.Access.Users.CustomUserSettings")]
public sealed class CustomUserSettingsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<CustomUserSettingsPermissions>();
        services.AddDeployment<CustomUserSettingsDeploymentSource, CustomUserSettingsDeploymentStep>();
        services.AddScoped<IStereotypesProvider, CustomUserSettingsStereotypesProvider>();

        services.Configure<ContentTypeDefinitionOptions>(options =>
        {
            options.Stereotypes.TryAdd("CustomUserSettings", new ContentTypeDefinitionDriverOptions
            {
                ShowCreatable = false,
                ShowListable = false,
                ShowDraftable = false,
                ShowVersionable = false,
            });
        });
    }
}

[RequireFeatures("Grove.Deployment")]
public sealed class UserDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AllUsersDeploymentSource, AllUsersDeploymentStep>();
    }
}
