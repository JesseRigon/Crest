#nullable enable

using Grove.Access;
using Grove.Settings;
using Grove.Users.Models;
using Microsoft.AspNetCore.Identity;

namespace Grove.Users.Services;

/// <summary>
/// A sign-in button per external identity provider the tenant has enabled (each provider module
/// registers an authentication scheme; nothing names them here). The button is a navigation to
/// the challenge (<c>api/grove/auth/external-login</c>); the platform's callback finishes it.
/// </summary>
public sealed class ExternalProviderLoginExtensions(SignInManager<IUser> signInManager) : ILoginExtensionProvider
{
    public async Task<IEnumerable<LoginExtension>> GetExtensionsAsync(LoginExtensionContext context)
    {
        if (context.Form != AccessForms.Login)
        {
            return [];
        }

        var schemes = await signInManager.GetExternalAuthenticationSchemesAsync();
        return schemes.Select((scheme, index) => new LoginExtension(
            "provider-" + scheme.Name,
            LoginExtensionKind.Provider,
            scheme.DisplayName ?? scheme.Name,
            Url: ExternalLoginUrl(context, scheme.Name),
            Order: index));
    }

    /// <summary>The navigation that starts an external sign-in with <paramref name="provider"/>.</summary>
    public static string ExternalLoginUrl(LoginExtensionContext context, string provider)
    {
        var url = context.TenantPath("/api/grove/auth/external-login") + "?provider=" + Uri.EscapeDataString(provider);
        return string.IsNullOrEmpty(context.ReturnUrl) ? url : url + "&returnUrl=" + Uri.EscapeDataString(context.ReturnUrl);
    }
}

/// <summary>"Forgot your password?" under the sign-in form, when the tenant allows resetting one.</summary>
public sealed class ResetPasswordLoginExtensions(ISiteService siteService) : ILoginExtensionProvider
{
    public async Task<IEnumerable<LoginExtension>> GetExtensionsAsync(LoginExtensionContext context)
    {
        if (context.Form != AccessForms.Login || !(await siteService.GetSettingsAsync<ResetPasswordSettings>()).AllowResetPassword)
        {
            return [];
        }

        return [new LoginExtension("forgot-password", LoginExtensionKind.Link, "Forgot your password?", context.TenantPath(AccountPages.ForgotPassword), Order: 5)];
    }
}

/// <summary>"Register as a new user" under the sign-in form, while registration is enabled.</summary>
public sealed class RegistrationLoginExtensions : ILoginExtensionProvider
{
    public Task<IEnumerable<LoginExtension>> GetExtensionsAsync(LoginExtensionContext context)
    {
        if (context.Form != AccessForms.Login)
        {
            return Task.FromResult<IEnumerable<LoginExtension>>([]);
        }

        var url = context.TenantPath(AccountPages.Register);
        if (!string.IsNullOrEmpty(context.ReturnUrl))
        {
            url += "?returnUrl=" + Uri.EscapeDataString(context.ReturnUrl);
        }

        return Task.FromResult<IEnumerable<LoginExtension>>([new LoginExtension("register", LoginExtensionKind.Link, "Register as a new user", url, Order: 10)]);
    }
}
