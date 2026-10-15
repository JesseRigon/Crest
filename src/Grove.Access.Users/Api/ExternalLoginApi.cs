#nullable enable

using System.Text.Json.Nodes;
using Grove.Access.Users.Events;
using Grove.Access.Users.Handlers;
using Grove.Access.Users.Models;
using Grove.Access.Users.Services;
using Grove.Access.Users.Workflows;
using Grove.Api;
using Grove.Communications.Email;
using Grove.Infrastructure.Settings;
using Grove.Modules;
using Grove.Routing;
using Grove.Security.RateLimits;
using Grove.Workflows;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Grove.Access.Users.Api;

/// <summary>A pending external sign-in, as the site shell's completion page shows it.</summary>
public sealed record PendingExternalLoginModel(
    string LoginProvider,
    string ProviderDisplayName,
    string UserName,
    string? Email,
    bool NoPassword,
    bool NoEmail,
    bool NoUsername,
    bool RegistrationDisabled,
    string? LinkUserName);

/// <summary>The outcome of completing an external sign-in.</summary>
public sealed record ExternalLoginResult(bool Succeeded, string[] Errors, string? Redirect);

/// <summary>What the completion page sends to register the account a pending external sign-in creates.</summary>
public sealed record ExternalLoginRegisterRequest(string? UserName, string? Email, string? Password, string? ConfirmPassword);

/// <summary>What the completion page sends to link a pending external sign-in to the account with its email.</summary>
public sealed record ExternalLoginLinkRequest(string? Password);

/// <summary>
/// The external sign-in's protocol endpoints, at the paths the providers' round trips already
/// carry (<see cref="CallbackPath"/>): the challenge (a form post) and the callback the provider's
/// handler returns to, which signs in, links or registers the account. No pages: a sign-in that
/// cannot finish here goes to the site shell's external login pages
/// (<see cref="ExternalLoginHandlers.RegisterPage"/>, <see cref="ExternalLoginHandlers.LinkPage"/>),
/// a refused one back to the login page with the message.
/// </summary>
public sealed class ExternalAuthenticationApi : ApiContributor
{
    /// <summary>The prefix the challenge and the callback keep.</summary>
    public const string Prefix = "Grove.Access.Users/ExternalAuthentications";

    /// <summary>The callback, tenant-relative: where every external challenge returns.</summary>
    public const string CallbackPath = "/" + Prefix + "/ExternalLoginCallback";

    public override string Name => "ExternalAuthentication";

    public override string FeatureId => UserConstants.Features.ExternalAuthentication;

    public override string RoutePrefix => Prefix;

    public override void Map(IEndpointRouteBuilder group)
    {
        // A form post from a sign-in page: no antiforgery header to carry (the challenge
        // starts a round trip to the provider, like the GET api/grove/auth/external-login).
        group.MapPost("ExternalLogin", ApiHandlers.Handle<ExternalLoginHandlers>(h => h.ChallengeAsync()))
            .AllowAnonymous()
            .SkipApiAntiforgery();
        group.MapGet("ExternalLoginCallback", (HttpContext http, [FromQuery] string? returnUrl, [FromQuery] string? remoteError) => ApiHandlers.For<ExternalLoginHandlers>(http).CallbackAsync(returnUrl, remoteError))
            .AllowAnonymous();
    }

    /// <summary>The callback URL a challenge names, under the request's path base.</summary>
    public static string CallbackUrl(HttpRequest request, string? returnUrl) =>
        request.PathBase + CallbackPath + (string.IsNullOrEmpty(returnUrl) ? string.Empty : "?returnUrl=" + Uri.EscapeDataString(returnUrl));
}

/// <summary>
/// The JSON completion of a pending external sign-in, for the site shell's external login pages:
/// what the provider sent and what the tenant still asks for, registering a new account, and
/// linking to the existing account with the same email.
/// </summary>
public sealed class ExternalLoginApi : ApiContributor
{
    public override string Name => "ExternalLogin";

    public override string FeatureId => UserConstants.Features.ExternalAuthentication;

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("account/external-login").AllowAnonymous();
        routes.MapGet("", ApiHandlers.Handle<ExternalLoginHandlers>(h => h.PendingAsync()));
        routes.MapPost("register", (HttpContext http, [FromBody] ExternalLoginRegisterRequest request, [FromQuery] string? returnUrl) => ApiHandlers.For<ExternalLoginHandlers>(http).RegisterAsync(request, returnUrl))
            .WithName(RateLimitRouteNames.RegisterExternalLogin)
            .WithRateLimitGroup(UserRateLimiterPolicyNames.UserRegistration);
        routes.MapPost("link", (HttpContext http, [FromBody] ExternalLoginLinkRequest request, [FromQuery] string? returnUrl) => ApiHandlers.For<ExternalLoginHandlers>(http).LinkAsync(request, returnUrl))
            .WithName(RateLimitRouteNames.LinkExternalLogin)
            .WithRateLimitGroup(UserRateLimiterPolicyNames.PasswordAuthentication);
    }
}

public sealed class ExternalLoginHandlers(
    SignInManager<IUser> signInManager,
    UserManager<IUser> userManager,
    ILogger<ExternalLoginHandlers> logger,
    ISiteService siteService,
    IStringLocalizer<ExternalLoginHandlers> localizer,
    IEnumerable<ILoginFormEvent> loginFormEvents,
    IEmailAddressValidator emailAddressValidator,
    Services.IUserService userService,
    IEnumerable<IExternalLoginEventHandler> externalLoginEventHandlers,
    IOptions<IdentityOptions> identityOptions,
    IOptions<UserOptions> userOptions,
    IShellLinkGenerator links) : ApiHandlers
{
    /// <summary>Where a pending external sign-in completes, on the site shell (Grove.Access.Users' site library).</summary>
    public const string RegisterPage = "/ExternalLogin/Register";

    /// <summary>Where a pending external sign-in links to the existing account with the same email.</summary>
    public const string LinkPage = "/ExternalLogin/Link";

    private readonly IStringLocalizer S = localizer;

    /// <summary>Starts an external sign-in from a form post (or a query): the provider's challenge.</summary>
    public async Task<IResult> ChallengeAsync()
    {
        var form = Request.HasFormContentType ? await Request.ReadFormAsync(HttpContext.RequestAborted) : null;
        string? Value(string key) => form is not null && form.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value)
            ? value.ToString()
            : Request.Query.TryGetValue(key, out var query) && !string.IsNullOrEmpty(query) ? query.ToString() : null;

        var provider = Value("provider");
        if (string.IsNullOrWhiteSpace(provider))
        {
            return ApiProblems.BadRequest("A provider is required.");
        }

        var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, ExternalAuthenticationApi.CallbackUrl(Request, Value("returnUrl")));
        if (bool.TryParse(Value("rememberMe"), out var rememberMe))
        {
            properties.Items[nameof(LoginForm.RememberMe)] = rememberMe.ToString();
        }

        return Results.Challenge(properties, [provider]);
    }

    /// <summary>
    /// The provider's return: signs in the account linked to the external login, sends an
    /// account with the same email to the link page, registers a new one when the tenant asks
    /// for nothing more, and otherwise sends the browser to the register page.
    /// </summary>
    public async Task<IResult> CallbackAsync(string? returnUrl, string? remoteError)
    {
        if (remoteError != null)
        {
            logger.LogError("Error from external provider: {Error}", remoteError);
            return ToLogin(returnUrl, S["An error occurred in external provider."]);
        }

        var info = await signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            logger.LogError("Could not get external login info.");
            return ToLogin(returnUrl, S["An error occurred in external provider."]);
        }

        var loginSettings = await siteService.GetSettingsAsync<LoginSettings>();
        var rememberMe = GetRememberMe(info, loginSettings);
        var errors = new List<string>();

        var iUser = await userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        if (iUser != null)
        {
            logger.LogInformation("Found user using external provider and provider key.");

            await loginFormEvents.InvokeAsync((e, user, errors) => e.LoggingInAsync(user.UserName, (key, message) => errors.Add(message)), iUser, errors, logger);
            if (errors.Count == 0)
            {
                if (await ValidateAsync(iUser) is { } refusal)
                {
                    return Refused(refusal, returnUrl);
                }

                if ((await ExternalSignInAsync(iUser, info, rememberMe, errors)).Succeeded)
                {
                    return Results.Redirect(await SignedInAsync(iUser, returnUrl, info));
                }
            }

            errors.Add(S["Invalid login attempt."]);
            return ToLogin(returnUrl, errors[0]);
        }

        var email = info.GetEmail();
        if (identityOptions.Value.User.RequireUniqueEmail && !string.IsNullOrWhiteSpace(email))
        {
            iUser = await userManager.FindByEmailAsync(email);
        }

        if (iUser != null)
        {
            logger.LogInformation("Found external user using email. Attempt to link them to existing user.");

            if (await ValidateAsync(iUser) is { } refusal)
            {
                return Refused(refusal, returnUrl);
            }

            // Link the external login to the existing user: the account page asks for its password.
            return ToExternalLoginPage(LinkPage, returnUrl);
        }

        var settings = await siteService.GetSettingsAsync<ExternalRegistrationSettings>();
        if (settings.DisableNewRegistrations)
        {
            await loginFormEvents.InvokeAsync(e => e.LoggingInFailedAsync("External User"), logger);
            return ToLogin(returnUrl, S["New registrations are disabled for this site."]);
        }

        // The user doesn't exist and no information is required: create the account locally
        // instead of asking on the register page.
        if (settings.NoPassword && settings.NoEmail && settings.NoUsername)
        {
            logger.LogInformation("Auto registering an external user using password-less method.");

            iUser = await userService.RegisterAsync(new RegisterUserForm
            {
                UserName = await GenerateUsernameAsync(info),
                Email = email,
                Password = null,
            }, (key, message) => errors.Add(message));

            if (iUser == null || errors.Count > 0)
            {
                logger.LogError("Unable to create internal account and link it to the external user.");
                return ToExternalLoginPage(RegisterPage, returnUrl, S["Unable to create internal account and link it to the external user."]);
            }

            var identityResult = await userManager.AddLoginAsync(iUser, new UserLoginInfo(info.LoginProvider, info.ProviderKey, info.ProviderDisplayName));
            if (identityResult.Succeeded)
            {
                logger.LogInformation(3, "User account linked to {LoginProvider} provider.", info.LoginProvider);

                // The login info must be linked before a refusal redirects, or it is lost.
                if (await ValidateAsync(iUser) is { } refusal)
                {
                    return Refused(refusal, returnUrl);
                }

                // The local user is created and linked: the sign-in must still succeed.
                if ((await ExternalSignInAsync(iUser, info, rememberMe, errors)).Succeeded)
                {
                    return Results.Redirect(await SignedInAsync(iUser, returnUrl, info));
                }

                errors.Add(S["Invalid login attempt."]);
                return ToLogin(returnUrl, errors[0]);
            }

            logger.LogError("Unable to add external provider to a user: {LoginProvider} provider.", info.LoginProvider);
            errors.AddRange(identityResult.Errors.Select(error => error.Description));
        }

        // The register page completes the registration; it reads what the provider sent from the
        // external cookie.
        return ToExternalLoginPage(RegisterPage, returnUrl, errors.FirstOrDefault());
    }

    /// <summary>
    /// The pending external sign-in the account page completes: who the provider says the user
    /// is, what the tenant's external registration settings still ask for, and - when an account
    /// with the same email exists - that account's user name to link to.
    /// </summary>
    public async Task<IResult> PendingAsync()
    {
        var info = await signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            return NotFound();
        }

        var settings = await siteService.GetSettingsAsync<ExternalRegistrationSettings>();
        var email = info.GetEmail();
        var existing = identityOptions.Value.User.RequireUniqueEmail && !string.IsNullOrWhiteSpace(email)
            ? await userManager.FindByEmailAsync(email)
            : null;

        return Ok(new PendingExternalLoginModel(
            info.LoginProvider,
            info.ProviderDisplayName ?? info.LoginProvider,
            await GenerateUsernameAsync(info),
            email,
            settings.NoPassword,
            settings.NoEmail,
            settings.NoUsername,
            settings.DisableNewRegistrations,
            existing?.UserName));
    }

    public async Task<IResult> RegisterAsync(ExternalLoginRegisterRequest request, string? returnUrl)
    {
        var settings = await siteService.GetSettingsAsync<ExternalRegistrationSettings>();
        if (settings.DisableNewRegistrations)
        {
            return BadRequest(Result(false, [S["New registrations are disabled for this site."]]));
        }

        var info = await signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            logger.LogWarning("Error loading external login info.");
            return NotFound();
        }

        var errors = new List<string>();
        var email = ValidateEmail(request.Email, info, settings.NoEmail, errors);
        var userName = await ValidateUserNameAsync(request.UserName, info, settings.NoUsername, errors);
        var password = await ValidatePasswordAsync(request.Password, request.ConfirmPassword, settings.NoPassword, errors);

        if (errors.Count == 0)
        {
            var iUser = await userService.RegisterAsync(
                new RegisterUserForm
                {
                    UserName = userName,
                    Email = email,
                    Password = password,
                }, (key, message) => errors.Add(message));

            if (iUser is not null && errors.Count == 0)
            {
                var identityResult = await userManager.AddLoginAsync(iUser, new UserLoginInfo(info.LoginProvider, info.ProviderKey, info.ProviderDisplayName));
                if (identityResult.Succeeded)
                {
                    logger.LogInformation(3, "User account linked to {LoginProvider} provider.", info.LoginProvider);

                    if (await ValidateAsync(iUser) is { } refusal)
                    {
                        return BadRequest(RefusalResult(refusal));
                    }

                    // The local user is created and linked: the sign-in must still succeed.
                    var loginSettings = await siteService.GetSettingsAsync<LoginSettings>();
                    if ((await ExternalSignInAsync(iUser, info, GetRememberMe(info, loginSettings), errors)).Succeeded)
                    {
                        return Ok(Result(true, errors, await SignedInAsync(iUser, returnUrl, info)));
                    }
                }

                errors.AddRange(identityResult.Errors.Select(error => error.Description));
            }
        }

        return BadRequest(Result(false, errors));
    }

    public async Task<IResult> LinkAsync(ExternalLoginLinkRequest request, string? returnUrl)
    {
        var info = await signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            logger.LogWarning("Error loading external login info.");
            return NotFound();
        }

        var user = await userManager.FindByEmailAsync(info.GetEmail());
        if (user == null)
        {
            logger.LogWarning("Suspicious login detected from external provider. {LoginProvider} with key [{ProviderKey}] for {Identity}",
                info.LoginProvider, info.ProviderKey, info.Principal?.Identity?.Name);

            return BadRequest(Result(false, [S["Invalid login attempt."]]));
        }

        var errors = new List<string>();
        if (string.IsNullOrEmpty(request.Password))
        {
            errors.Add(S["Password is required."]);
        }

        await loginFormEvents.InvokeAsync((e, user, errors) => e.LoggingInAsync(user.UserName, (key, message) => errors.Add(message)), user, errors, logger);
        if (errors.Count == 0)
        {
            var signInResult = await signInManager.CheckPasswordSignInAsync(user, request.Password!, false);
            if (!signInResult.Succeeded)
            {
                return BadRequest(Result(false, [S["Invalid login attempt."]]));
            }

            var identityResult = await userManager.AddLoginAsync(user, new UserLoginInfo(info.LoginProvider, info.ProviderKey, info.ProviderDisplayName));
            if (identityResult.Succeeded)
            {
                logger.LogInformation(3, "User account linked to {LoginProvider} provider.", info.LoginProvider);

                // The local user is linked: the sign-in must still succeed.
                var loginSettings = await siteService.GetSettingsAsync<LoginSettings>();
                if ((await ExternalSignInAsync(user, info, GetRememberMe(info, loginSettings), errors)).Succeeded)
                {
                    return Ok(Result(true, errors, await SignedInAsync(user, returnUrl, info)));
                }
            }

            errors.AddRange(identityResult.Errors.Select(error => error.Description));
        }

        return BadRequest(Result(false, errors));
    }

    private static ExternalLoginResult Result(bool succeeded, IEnumerable<string> errors, string? redirect = null) =>
        new(succeeded, [.. errors.Where(message => !string.IsNullOrEmpty(message))], redirect);

    // A refusal that names a page sends the browser there (the page explains); one that does not
    // is shown on the completion page.
    private ExternalLoginResult RefusalResult(LoginRefusal refusal) => refusal.Redirect is not null
        ? Result(false, [], LoginService.ResolveRedirect(Request, refusal.Redirect))
        : Result(false, refusal.Errors.Count > 0 ? refusal.Errors : [S["Invalid login attempt."]]);

    private async Task<LoginRefusal?> ValidateAsync(IUser user)
    {
        foreach (var loginFormEvent in loginFormEvents)
        {
            if (await loginFormEvent.ValidatingLoginAsync(user) is { } refusal)
            {
                return refusal;
            }
        }

        return null;
    }

    private IResult Refused(LoginRefusal refusal, string? returnUrl) => refusal.Redirect is not null
        ? Results.Redirect(LoginService.ResolveRedirect(Request, refusal.Redirect)!)
        : ToLogin(returnUrl, refusal.Errors.FirstOrDefault() ?? S["Invalid login attempt."]);

    private async Task<string?> ValidatePasswordAsync(string? password, string? confirmPassword, bool noPassword, List<string> errors)
    {
        if (noPassword)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add(S["Password is required!"]);
        }
        else if (password != confirmPassword)
        {
            errors.Add(S["Confirm Password do not match"]);
        }
        else if (userManager.PasswordValidators != null)
        {
            var user = new User();
            foreach (var passwordValidator in userManager.PasswordValidators)
            {
                var validationResult = await passwordValidator.ValidateAsync(userManager, user, password);
                if (!validationResult.Succeeded)
                {
                    errors.AddRange(validationResult.Errors.Select(error => error.Description));
                }
            }
        }

        return password;
    }

    private async Task<string?> ValidateUserNameAsync(string? userName, ExternalLoginInfo info, bool noUsername, List<string> errors)
    {
        if (noUsername && string.IsNullOrWhiteSpace(userName))
        {
            userName = await GenerateUsernameAsync(info);
        }

        if (string.IsNullOrWhiteSpace(userName))
        {
            errors.Add(S["Username is required!"]);
        }

        return userName;
    }

    private string? ValidateEmail(string? email, ExternalLoginInfo info, bool noEmail, List<string> errors)
    {
        if (noEmail && string.IsNullOrWhiteSpace(email))
        {
            email = info.GetEmail();
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            errors.Add(S["Email is required!"]);
        }
        else if (!emailAddressValidator.Validate(email))
        {
            errors.Add(S["Invalid Email."]);
        }

        return email;
    }

    private async Task<Microsoft.AspNetCore.Identity.SignInResult> ExternalSignInAsync(IUser user, ExternalLoginInfo info, bool rememberMe, List<string> errors)
    {
        logger.LogInformation("Attempting to do an external sign in.");

        var externalClaims = info.Principal.GetSerializableClaims().ToArray();
        var userRoles = await userManager.GetRolesAsync(user);
        var userInfo = (User)user;

        var context = new UpdateUserContext(user, info.LoginProvider, externalClaims, userInfo.Properties.DeepClone() as JsonObject)
        {
            UserClaims = userInfo.UserClaims,
            UserRoles = userRoles,
        };

        foreach (var externalLoginEventHandler in externalLoginEventHandlers)
        {
            try
            {
                await externalLoginEventHandler.UpdateUserAsync(context);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "The method {ExternalLoginHandler}.UpdateUserAsync(context) threw an exception", externalLoginEventHandler.GetType());
            }
        }

        if (await userManager.UpdateUserPropertiesAsync(userInfo, context))
        {
            await userManager.UpdateAsync(user);
        }

        var loginSettings = await siteService.GetSettingsAsync<LoginSettings>();
        var isPersistent = loginSettings.AllowRememberMe
            ? rememberMe
            : loginSettings.UsePersistentAuthenticationCookie;

        var result = await signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent, bypassTwoFactor: true);
        if (result.Succeeded)
        {
            await loginFormEvents.InvokeAsync((e, user) => e.LoggedInAsync(user), user, logger);

            var identityResult = await signInManager.UpdateExternalAuthenticationTokensAsync(info);
            if (!identityResult.Succeeded)
            {
                logger.LogError("Error updating the external authentication tokens.");
                errors.AddRange(identityResult.Errors.Select(error => error.Description));
            }
        }
        else
        {
            await loginFormEvents.InvokeAsync((e, user) => e.LoggingInFailedAsync(user), user, logger);
        }

        return result;
    }

    private static bool GetRememberMe(ExternalLoginInfo info, LoginSettings loginSettings)
    {
        if (info?.AuthenticationProperties?.Items is not null &&
            info.AuthenticationProperties.Items.TryGetValue(nameof(LoginForm.RememberMe), out var rememberMe) &&
            bool.TryParse(rememberMe, out var isRememberMe))
        {
            return isRememberMe;
        }

        return loginSettings.UsePersistentAuthenticationCookie;
    }

    // Back to the staff login page with the error (the page shows it, and does not start the
    // only provider again).
    private IResult ToLogin(string? returnUrl, string? error)
    {
        var query = new List<string> { "externalLoginError=true" };
        if (links.IsLocalUrl(returnUrl))
        {
            query.Add("returnUrl=" + Uri.EscapeDataString(returnUrl!));
        }

        if (!string.IsNullOrEmpty(error))
        {
            query.Add("error=" + Uri.EscapeDataString(error));
        }

        return Results.Redirect(links.Content("~/" + userOptions.Value.LoginPath + "?" + string.Join('&', query)));
    }

    private IResult ToExternalLoginPage(string page, string? returnUrl, string? error = null)
    {
        var query = new List<string>();
        if (links.IsLocalUrl(returnUrl))
        {
            query.Add("returnUrl=" + Uri.EscapeDataString(returnUrl!));
        }

        if (!string.IsNullOrEmpty(error))
        {
            query.Add("error=" + Uri.EscapeDataString(error));
        }

        return Results.Redirect(links.Content("~" + page + (query.Count > 0 ? "?" + string.Join('&', query) : string.Empty)));
    }

    // The signed-in user's next page (the local return URL, else the tenant's home), after the
    // user.logged-in trigger.
    private async Task<string> SignedInAsync(IUser user, string? returnUrl, ExternalLoginInfo info)
    {
        var triggers = HttpContext.RequestServices.GetService<IWorkflowTriggerPublisher>();
        if (triggers is not null && user is User platformUser)
        {
            // The flows act as the user who just signed in, not the anonymous request (docs/access.md, Anonymous actors).
            await triggers.PublishForUserAsync(UserWorkflowTriggers.LoggedIn, platformUser.UserId, platformUser.UserId, platformUser.UserName, UserWorkflowTriggers.Payload(platformUser, info.LoginProvider ?? string.Empty));
        }

        return links.IsLocalUrl(returnUrl) ? links.Content(returnUrl!.ToUriComponents()) : links.Content("~/");
    }

    private async Task<string> GenerateUsernameAsync(ExternalLoginInfo info)
    {
        var ret = string.Concat("u", IdGenerator.GenerateId());
        var externalClaims = info.Principal.GetSerializableClaims().ToArray();
        var userNames = new Dictionary<Type, string>();

        foreach (var externalLoginEventHandler in externalLoginEventHandlers)
        {
            try
            {
                var userName = await externalLoginEventHandler.GenerateUserName(info.LoginProvider, externalClaims);
                if (!string.IsNullOrWhiteSpace(userName))
                {
                    // The first handler's user name wins.
                    if (userNames.Count == 0)
                    {
                        ret = userName;
                    }

                    userNames.Add(externalLoginEventHandler.GetType(), userName);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{ExternalLoginHandler} - IExternalLoginHandler.GenerateUserName threw an exception", externalLoginEventHandler.GetType());
            }
        }

        if (userNames.Count > 1)
        {
            logger.LogWarning("More than one IExternalLoginHandler generated username. Used first one registered, {ExternalLoginHandler}", userNames.FirstOrDefault().Key);
        }

        return ret;
    }
}
