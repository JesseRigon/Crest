#nullable enable

using System.Text.Encodings.Web;
using Fluid.Values;
using Grove.Email;
using Grove.Liquid;
using Grove.Settings;
using Grove.Sms;
using Grove.Users.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Grove.Users.Services;

/// <summary>
/// Sends the one-time codes of the two-factor methods that deliver one: the email and SMS sign-in
/// codes, and the code that confirms a phone number for the SMS method. The message bodies are the
/// tenant's templates (the method's login settings), rendered with the user and the code.
/// </summary>
public sealed class TwoFactorCodeSender(
    UserManager<IUser> userManager,
    ISiteService siteService,
    IOptions<IdentityOptions> identityOptions,
    IServiceProvider services,
    HtmlEncoder htmlEncoder,
    IStringLocalizer<TwoFactorCodeSender> localizer)
{
    private readonly IStringLocalizer S = localizer;

    /// <summary>Sends <paramref name="user"/> the sign-in code of <paramref name="provider"/> (Email or Phone).</summary>
    public async Task<(bool Sent, string Message)> SendSignInCodeAsync(IUser user, string provider)
    {
        if (string.Equals(provider, TokenOptions.DefaultEmailProvider, StringComparison.Ordinal))
        {
            var failure = S["The email could not be sent. Please attempt to request the code at a later time."].Value;
            if (services.GetService<IEmailService>() is not { } email || await userManager.GetEmailAsync(user) is not { Length: > 0 } to)
            {
                return (false, failure);
            }

            var settings = await siteService.GetSettingsAsync<EmailAuthenticatorLoginSettings>();
            var code = await userManager.GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider);
            var subject = await RenderAsync(string.IsNullOrWhiteSpace(settings.Subject) ? EmailAuthenticatorLoginSettings.DefaultSubject : settings.Subject, user, code);
            var body = await RenderAsync(string.IsNullOrWhiteSpace(settings.Body) ? EmailAuthenticatorLoginSettings.DefaultBody : settings.Body, user, code);
            var result = await email.SendAsync(to, subject, body);

            return result.Succeeded
                ? (true, S["A verification code has been sent via email. Please check your email for the code."].Value)
                : (false, failure);
        }

        if (string.Equals(provider, TokenOptions.DefaultPhoneProvider, StringComparison.Ordinal))
        {
            var failure = S["The SMS message could not be sent. Please attempt to request the code at a later time."].Value;
            if (await userManager.GetPhoneNumberAsync(user) is not { Length: > 0 } phoneNumber)
            {
                return (false, failure);
            }

            var code = await userManager.GenerateTwoFactorTokenAsync(user, identityOptions.Value.Tokens.ChangePhoneNumberTokenProvider);
            return await SendSmsAsync(user, phoneNumber, code)
                ? (true, S["A verification code has been sent to your phone number. Please check your device for the code."].Value)
                : (false, failure);
        }

        return (false, S["This method does not send codes."].Value);
    }

    /// <summary>Sends the code that confirms <paramref name="phoneNumber"/> for the SMS method.</summary>
    public async Task<bool> SendPhoneConfirmationAsync(IUser user, string phoneNumber)
    {
        var code = await userManager.GenerateChangePhoneNumberTokenAsync(user, phoneNumber);
        return await SendSmsAsync(user, phoneNumber, code);
    }

    private async Task<bool> SendSmsAsync(IUser user, string phoneNumber, string code)
    {
        if (services.GetService<ISmsService>() is not { } sms)
        {
            return false;
        }

        var settings = await siteService.GetSettingsAsync<SmsAuthenticatorLoginSettings>();
        var body = await RenderAsync(string.IsNullOrWhiteSpace(settings.Body) ? EmailAuthenticatorLoginSettings.DefaultBody : settings.Body, user, code);
        return (await sms.SendAsync(phoneNumber, body)).Succeeded;
    }

    // The template rendered with the user and the code; without Liquid, the code itself.
    private async Task<string> RenderAsync(string template, IUser user, string code)
    {
        if (services.GetService<ILiquidTemplateManager>() is not { } liquid)
        {
            return code;
        }

        var content = await liquid.RenderHtmlContentAsync(template, htmlEncoder, null, new Dictionary<string, FluidValue>
        {
            ["User"] = new ObjectValue(user),
            ["Code"] = new StringValue(code),
        });

        using var writer = new StringWriter();
        content.WriteTo(writer, htmlEncoder);
        return writer.ToString();
    }
}
