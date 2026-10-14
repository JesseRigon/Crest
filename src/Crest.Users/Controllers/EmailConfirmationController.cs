using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Crest.DisplayManagement.Notify;
using Crest.Modules;
using Crest.Mvc.Utilities;
using Crest.Users.Handlers;
using Crest.Users.Models;
using Crest.Users.Services;

namespace Crest.Users.Controllers;

// Only the resend remains MVC (the stock two-factor and legacy user-list screens post to it);
// confirming an email and the "confirmation sent" message are the site shell's account pages
// (step 7), and the account API resends for the native pages.
public sealed class EmailConfirmationController : Controller
{
    private readonly UserManager<IUser> _userManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly UserEmailService _userEmailService;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public EmailConfirmationController(
        UserManager<IUser> userManager,
        IAuthorizationService authorizationService,
        INotifier notifier,
        UserEmailService userEmailService,
        IHtmlLocalizer<EmailConfirmationController> htmlLocalizer,
        IStringLocalizer<EmailConfirmationController> stringLocalizer)
    {
        _userManager = userManager;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _userEmailService = userEmailService;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> SendVerificationEmail(string id = null, string returnUrl = null)
    {
        var currentUserId = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(id))
        {
            id = currentUserId;
        }

        if (string.IsNullOrEmpty(id))
        {
            return NotFound();
        }

        // Allow users to verify their own email without the 'ManageUsers' permission.
        if (id != currentUserId && !await _authorizationService.AuthorizeAsync(User, UsersPermissions.ManageUsers))
        {
            return Forbid();
        }

        var user = await _userManager.FindByIdAsync(id) as User;

        if (user == null)
        {
            return NotFound();
        }

        await _userEmailService.SendEmailConfirmationAsync(user);

        await _notifier.SuccessAsync(H["Verification email sent."]);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return LocalRedirect($"~/{HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<Crest.Admin.AdminOptions>>().Value.AdminUrlPrefix.Trim('/')}/Users/Index");
    }
}
