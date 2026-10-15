using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Modules;
using Grove.Security.RateLimits;
using Grove.Security.Permissions;
using Grove.Access.Users.Events;
using Grove.Access.Users.Models;
using Grove.Access.Users.Services;

namespace Grove.Access.Users;

// The two-factor pages are the site shell's (Grove.Access.Users' site library: the two-factor page and
// the second sign-in step), and the login page asks for the code itself; these startups declare
// the methods, the two-factor API and the code sender.
[Feature(UserConstants.Features.TwoFactorAuthentication)]
public sealed class TwoFactorAuthenticationStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IUserClaimsProvider, TwoFactorAuthenticationClaimsProvider>();
        services.AddPermissionProvider<TwoFactorPermissionProvider>();
        services.AddScoped<TwoFactorCodeSender>();
        services.AddApiContributor<Api.TwoFactorApi>();

        // A user who must set it up reaches the two-factor page and nothing else (the access gate).
        services.AddScoped<Grove.Access.IPendingSignInStepProvider, TwoFactorSetupStep>();
    }
}

[Feature(UserConstants.Features.TwoFactorAuthentication)]
[RequireFeatures("Grove.Security.RateLimits")]
public sealed class TwoFactorAuthenticationRateLimitsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<RateLimitsOptions>(options =>
        {
            options.AddRouteRateLimit(RateLimitRouteNames.LoginWithTwoFactorAuthentication, HttpMethods.Post, RateLimitPartitionHelpers.CreateSlidingWindowPerIpPolicy(UserRateLimiterPolicyNames.TwoFactorAuthentication, 5));
            options.AddRouteRateLimit(RateLimitRouteNames.LoginWithRecoveryCode, HttpMethods.Post, RateLimitPartitionHelpers.CreateSlidingWindowPerIpPolicy(UserRateLimiterPolicyNames.TwoFactorRecovery, 3));
            options.AddRouteRateLimit(RateLimitRouteNames.TwoFactorSendCode, HttpMethods.Post, RateLimitPartitionHelpers.CreateFixedWindowPerIpPolicy(UserRateLimiterPolicyNames.TwoFactorCodeSend, 2, TimeSpan.FromMinutes(1)));
        });
    }
}

[RequireFeatures("Grove.Access.Roles", UserConstants.Features.TwoFactorAuthentication)]
public sealed class RoleTwoFactorAuthenticationStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ITwoFactorAuthenticationHandler, RoleTwoFactorAuthenticationHandler>();
    }
}

[Feature(UserConstants.Features.AuthenticatorApp)]
[RequireFeatures(UserConstants.Features.TwoFactorAuthentication)]
public sealed class AuthenticatorAppStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        var authenticatorProviderType = typeof(AuthenticatorTokenProvider<>).MakeGenericType(typeof(IUser));
        services.AddTransient(authenticatorProviderType);

        services.Configure<IdentityOptions>(options =>
        {
            options.Tokens.AuthenticatorTokenProvider = TokenOptions.DefaultAuthenticatorProvider;
            options.Tokens.ProviderMap[TokenOptions.DefaultAuthenticatorProvider] = new TokenProviderDescriptor(authenticatorProviderType);
        });

        services.AddTransient<IConfigureOptions<TwoFactorOptions>, AuthenticatorAppProviderTwoFactorOptionsConfiguration>();
    }
}

[Feature(UserConstants.Features.EmailAuthenticator)]
public sealed class EmailAuthenticatorStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        var emailProviderType = typeof(EmailTokenProvider<>).MakeGenericType(typeof(IUser));
        services.AddTransient(emailProviderType)
            .Configure<TwoFactorOptions>(options => options.Providers.Add(TokenOptions.DefaultEmailProvider))
            .Configure<IdentityOptions>(options =>
            {
                options.Tokens.ProviderMap[TokenOptions.DefaultEmailProvider] = new TokenProviderDescriptor(emailProviderType);
            });
    }
}

[Feature(UserConstants.Features.SmsAuthenticator)]
public sealed class SmsAuthenticatorStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        var phoneNumberProviderType = typeof(PhoneNumberTokenProvider<>).MakeGenericType(typeof(IUser));
        services.AddTransient(phoneNumberProviderType);
        services.Configure<IdentityOptions>(options =>
        {
            options.Tokens.ChangePhoneNumberTokenProvider = TokenOptions.DefaultPhoneProvider;
            options.Tokens.ProviderMap[TokenOptions.DefaultPhoneProvider] = new TokenProviderDescriptor(phoneNumberProviderType);
        });

        services.AddTransient<IConfigureOptions<TwoFactorOptions>, PhoneProviderTwoFactorOptionsConfiguration>();
    }
}
