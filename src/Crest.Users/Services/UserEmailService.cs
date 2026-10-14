using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Crest.DisplayManagement.Components;
using Crest.Email;
using Crest.Mvc.Utilities;
using Crest.Users.Controllers;
using Crest.Users.Models;

namespace Crest.Users.Services;

/// <summary>
/// The account emails: the address confirmation and the password reset. Their bodies are
/// templates (step 7: the Razor <c>TemplateUserConfirmEmail</c> and <c>TemplateUserLostPassword</c>
/// shapes are retired) - Templates content a tenant may author, named
/// <see cref="ConfirmEmailTemplate"/> and <see cref="LostPasswordTemplate"/>, with shipped defaults
/// (Crest.Server's AccountEmailTemplates) - rendered to HTML by <see cref="ITemplateHtmlRenderer"/>.
/// Without the Templates module the body is a plain built-in text.
/// </summary>
/// <remarks>
/// The template's <c>Model</c>: <c>UserName</c>, <c>Email</c>, <c>Url</c> (the confirmation or
/// reset link) and <c>ExpiresInMinutes</c>.
/// </remarks>
public sealed class UserEmailService
{
    /// <summary>The address confirmation's template.</summary>
    public const string ConfirmEmailTemplate = "UserConfirmEmail";

    /// <summary>The password reset's template.</summary>
    public const string LostPasswordTemplate = "UserLostPassword";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;
    private readonly UserManager<IUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly IServiceProvider _services;
    private readonly HtmlEncoder _htmlEncoder;

    internal readonly IStringLocalizer S;

    public UserEmailService(
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator,
        UserManager<IUser> userManager,
        IEmailService emailService,
        IServiceProvider services,
        HtmlEncoder htmlEncoder,
        IStringLocalizer<UserEmailService> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
        _userManager = userManager;
        _emailService = emailService;
        _services = services;
        _htmlEncoder = htmlEncoder;
        S = stringLocalizer;
    }

    public async Task<bool> SendEmailConfirmationAsync(IUser user)
    {
        var confirmEmailUrl = AccountPages.ConfirmEmailUrl(
            _httpContextAccessor.HttpContext.Request,
            await _userManager.GetUserIdAsync(user),
            await _userManager.GenerateEmailConfirmationTokenAsync(user));

        var email = await _userManager.GetEmailAsync(user);

        if (string.IsNullOrEmpty(email))
        {
            return false;
        }

        var lifespan = _services.GetService<IOptions<EmailConfirmationTokenProviderOptions>>()?.Value.TokenLifespan ?? TimeSpan.FromDays(1);
        var body = await RenderAsync(ConfirmEmailTemplate, user, email, confirmEmailUrl, lifespan, () =>
            $"<p>{Encode(S["Dear {0},", user.UserName])}</p>" +
            $"<p><a href=\"{_htmlEncoder.Encode(confirmEmailUrl)}\">{Encode(S["Confirm your account"])}</a></p>");

        return await SendAsync(email, S["Confirm your account"], body);
    }

    public async Task<bool> SendPasswordResetAsync(User user)
    {
        user.ResetToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(user.ResetToken));

        var lostPasswordUrl = _linkGenerator.GetUriByAction(
            httpContext: _httpContextAccessor.HttpContext,
            action: nameof(ResetPasswordController.ResetPassword),
            controller: typeof(ResetPasswordController).ControllerName(),
            values: new
            {
                userId = await _userManager.GetUserIdAsync(user),
                code = user.ResetToken,
            });

        return await SendPasswordResetEmailAsync(user, lostPasswordUrl);
    }

    /// <summary>
    /// Sends the password-reset email with a link to a reset page other than the tenant's own
    /// (the member portal's): <paramref name="resetUrl"/> builds the absolute link from the
    /// user id and the encoded reset code.
    /// </summary>
    public async Task<bool> SendPasswordResetAsync(User user, Func<string, string, string> resetUrl)
    {
        user.ResetToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(user.ResetToken));

        return await SendPasswordResetEmailAsync(user, resetUrl(await _userManager.GetUserIdAsync(user), user.ResetToken));
    }

    private async Task<bool> SendPasswordResetEmailAsync(User user, string lostPasswordUrl)
    {
        var lifespan = _services.GetService<IOptions<PasswordResetTokenProviderOptions>>()?.Value.TokenLifespan ?? TimeSpan.FromDays(1);
        var body = await RenderAsync(LostPasswordTemplate, user, user.Email, lostPasswordUrl, lifespan, () =>
            $"<p>{Encode(S["Someone recently requested that the password be reset for {0}.", user.UserName])}</p>" +
            $"<p><a href=\"{_htmlEncoder.Encode(lostPasswordUrl)}\">{Encode(S["Reset your password"])}</a></p>");

        return await SendAsync(user.Email, S["Reset your password"], body);
    }

    // The template's HTML, else the built-in text (no Templates module, or no template of the
    // name - the shipped defaults normally answer).
    private async Task<string> RenderAsync(string template, IUser user, string email, string url, TimeSpan lifespan, Func<string> fallback)
    {
        var model = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["UserName"] = user.UserName,
            ["Email"] = email,
            ["Url"] = url,
            ["ExpiresInMinutes"] = Convert.ToInt32(lifespan.TotalMinutes),
        };

        return _services.GetService<ITemplateHtmlRenderer>() is { } renderer && await renderer.RenderAsync(template, model) is { Length: > 0 } html
            ? html
            : fallback();
    }

    private string Encode(string text) => _htmlEncoder.Encode(text);

    private async Task<bool> SendAsync(string email, string subject, string body) =>
        (await _emailService.SendAsync(email, subject, body)).Succeeded;
}
