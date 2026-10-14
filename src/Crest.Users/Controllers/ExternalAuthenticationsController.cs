using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Crest.DisplayManagement.Notify;
using Crest.Email;
using Crest.Environment.Shell;
using Crest.Modules;
using Crest.Mvc.Utilities;
using Crest.RateLimits;
using Crest.Settings;
using Crest.Users.Events;
using Crest.Users.Handlers;
using Crest.Users.Models;
using Crest.Users.Services;
using Crest.Users.ViewModels;

namespace Crest.Users.Controllers;

/// <summary>A pending external sign-in, as the site shell's completion page shows it.</summary>
public sealed record PendingExternalLoginModel(
    string LoginProvider,
    string ProviderDisplayName,
    string UserName,
    string Email,
    bool NoPassword,
    bool NoEmail,
    bool NoUsername,
    bool RegistrationDisabled,
    string LinkUserName);

/// <summary>The outcome of completing an external sign-in.</summary>
public sealed record ExternalLoginResult(bool Succeeded, string[] Errors, string Redirect);

[Feature(UserConstants.Features.ExternalAuthentication)]
public sealed class ExternalAuthenticationsController : AccountBaseController
{
    private readonly SignInManager<IUser> _signInManager;
    private readonly UserManager<IUser> _userManager;
    private readonly ILogger _logger;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ISiteService _siteService;
    private readonly IEnumerable<ILoginFormEvent> _loginFormEvents;
    private readonly IShellFeaturesManager _shellFeaturesManager;
    private readonly IEmailAddressValidator _emailAddressValidator;
    private readonly IUserService _userService;
    private readonly INotifier _notifier;
    private readonly IEnumerable<IExternalLoginEventHandler> _externalLoginEventHandlers;
    private readonly IdentityOptions _identityOptions;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public ExternalAuthenticationsController(
        SignInManager<IUser> signInManager,
        UserManager<IUser> userManager,
        ILogger<ExternalAuthenticationsController> logger,
        IDataProtectionProvider dataProtectionProvider,
        ISiteService siteService,
        IHtmlLocalizer<ExternalAuthenticationsController> htmlLocalizer,
        IStringLocalizer<ExternalAuthenticationsController> stringLocalizer,
        IEnumerable<ILoginFormEvent> loginFormEvents,
        IShellFeaturesManager shellFeaturesManager,
        IEmailAddressValidator emailAddressValidator,
        IUserService userService,
        INotifier notifier,
        IEnumerable<IExternalLoginEventHandler> externalLoginEventHandlers,
        IOptions<IdentityOptions> identityOptions)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
        _dataProtectionProvider = dataProtectionProvider;
        _siteService = siteService;
        _loginFormEvents = loginFormEvents;
        _shellFeaturesManager = shellFeaturesManager;
        _emailAddressValidator = emailAddressValidator;
        _userService = userService;
        _notifier = notifier;
        _externalLoginEventHandlers = externalLoginEventHandlers;
        _identityOptions = identityOptions.Value;

        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [HttpPost]
    [AllowAnonymous]
    public IActionResult ExternalLogin(string provider, bool? rememberMe = null, string returnUrl = null)
    {
        // Request a redirect to the external login provider.
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);

        if (rememberMe.HasValue)
        {
            properties.Items[nameof(LoginForm.RememberMe)] = rememberMe.ToString();
        }

        return Challenge(properties, provider);
    }

    [AllowAnonymous]
    public async Task<IActionResult> ExternalLoginCallback(string returnUrl = null, string remoteError = null)
    {
        if (remoteError != null)
        {
            _logger.LogError("Error from external provider: {Error}", remoteError);

            ModelState.AddModelError(string.Empty, S["An error occurred in external provider."]);

            return RedirectToLogin(returnUrl);
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();

        if (info == null)
        {
            _logger.LogError("Could not get external login info.");

            ModelState.AddModelError(string.Empty, S["An error occurred in external provider."]);

            return RedirectToLogin(returnUrl);
        }

        var loginSettings = await _siteService.GetSettingsAsync<LoginSettings>();

        var rememberMe = GetRememberMe(info, loginSettings);

        var iUser = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);

        CopyTempDataErrorsToModelState();

        if (iUser != null)
        {
            _logger.LogInformation("Found user using external provider and provider key.");

            await _loginFormEvents.InvokeAsync((e, user, modelState) => e.LoggingInAsync(user.UserName, (key, message) => modelState.AddModelError(key, message)), iUser, ModelState, _logger);

            if (ModelState.IsValid)
            {
                foreach (var loginFormEvent in _loginFormEvents)
                {
                    var loginResult = await loginFormEvent.ValidatingLoginAsync(iUser);

                    if (loginResult != null)
                    {
                        return loginResult;
                    }
                }

                var signInResult = await ExternalSignInAsync(iUser, info, rememberMe);

                if (signInResult.Succeeded)
                {
                    return await LoggedInActionResultAsync(iUser, returnUrl, info);
                }
            }

            ModelState.AddModelError(string.Empty, S["Invalid login attempt."]);

            return RedirectToLogin(returnUrl);
        }

        var email = info.GetEmail();

        if (_identityOptions.User.RequireUniqueEmail && !string.IsNullOrWhiteSpace(email))
        {
            iUser = await _userManager.FindByEmailAsync(email);
        }

        ViewData["ReturnUrl"] = returnUrl;
        ViewData["LoginProvider"] = info.LoginProvider;

        if (iUser != null)
        {
            _logger.LogInformation("Found external user using email. Attempt to link them to existing user.");

            foreach (var loginFormEvent in _loginFormEvents)
            {
                var loginResult = await loginFormEvent.ValidatingLoginAsync(iUser);

                if (loginResult != null)
                {
                    return loginResult;
                }
            }

            // Link external login to an existing user: the account page asks for its password
            // (step 7: the MVC LinkExternalLogin view is retired).
            return RedirectToExternalLoginPage(LinkPage, returnUrl);
        }

        var settings = await _siteService.GetSettingsAsync<ExternalRegistrationSettings>();

        if (settings.DisableNewRegistrations)
        {
            await _notifier.ErrorAsync(H["New registrations are disabled for this site."]);

            await _loginFormEvents.InvokeAsync(e => e.LoggingInFailedAsync("External User"), _logger);

            return RedirectToLogin(returnUrl);
        }

        var externalLoginViewModel = new RegisterExternalLoginViewModel
        {
            NoPassword = settings.NoPassword,
            NoEmail = settings.NoEmail,
            NoUsername = settings.NoUsername,

            UserName = await GenerateUsernameAsync(info),
            Email = email,
        };

        // The user doesn't exist and no information required, we can create the account locally
        // instead of redirecting to the ExternalLogin.
        var noInformationRequired = settings.NoPassword &&
            settings.NoEmail &&
            settings.NoUsername;

        if (noInformationRequired)
        {
            _logger.LogInformation("Auto registering an external user using password-less method.");

            iUser = await _userService.RegisterAsync(new RegisterUserForm()
            {
                UserName = externalLoginViewModel.UserName,
                Email = externalLoginViewModel.Email,
                Password = null,
            }, ModelState.AddModelError);

            // If the registration was successful we can link the external provider and redirect the user.
            if (iUser == null || !ModelState.IsValid)
            {
                _logger.LogError("Unable to create internal account and link it to the external user.");

                return RedirectToExternalLoginPage(RegisterPage, returnUrl, S["Unable to create internal account and link it to the external user."]);
            }

            var identityResult = await _userManager.AddLoginAsync(iUser, new UserLoginInfo(info.LoginProvider, info.ProviderKey, info.ProviderDisplayName));

            if (identityResult.Succeeded)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(3, "User account linked to {LoginProvider} provider.", info.LoginProvider);
                }

                // The login info must be linked before we consider a redirect, or the login info is lost.
                if (iUser is User user)
                {
                    foreach (var loginFormEvent in _loginFormEvents)
                    {
                        var loginResult = await loginFormEvent.ValidatingLoginAsync(user);

                        if (loginResult != null)
                        {
                            return loginResult;
                        }
                    }
                }

                // We have created/linked to the local user, so we must verify the login.
                // If it does not succeed, the user is not allowed to login
                var signInResult = await ExternalSignInAsync(iUser, info, rememberMe);

                if (signInResult.Succeeded)
                {
                    return await LoggedInActionResultAsync(iUser, returnUrl, info);
                }

                ModelState.AddModelError(string.Empty, S["Invalid login attempt."]);

                return RedirectToLogin(returnUrl);
            }

            _logger.LogError("Unable to add external provider to a user: {LoginProvider} provider.", info.LoginProvider);

            AddErrorsToModelState(identityResult.Errors);
        }

        // The account page completes the registration (step 7: the MVC RegisterExternalLogin
        // view is retired); it reads what the provider sent from the external cookie.
        return RedirectToExternalLoginPage(RegisterPage, returnUrl, ModelState.Values.SelectMany(state => state.Errors).FirstOrDefault()?.ErrorMessage);
    }

    /// <summary>Where a pending external sign-in completes, on the site shell (Crest.SiteTheme.Client).</summary>
    public const string RegisterPage = "/ExternalLogin/Register";

    /// <summary>Where a pending external sign-in links to the existing account with the same email.</summary>
    public const string LinkPage = "/ExternalLogin/Link";

    private IActionResult RedirectToExternalLoginPage(string page, string returnUrl, string error = null)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            query.Add("returnUrl=" + Uri.EscapeDataString(returnUrl));
        }

        if (!string.IsNullOrEmpty(error))
        {
            query.Add("error=" + Uri.EscapeDataString(error));
        }

        return LocalRedirect("~" + page + (query.Count > 0 ? "?" + string.Join('&', query) : string.Empty));
    }

    /// <summary>
    /// The pending external sign-in the account page completes: who the provider says the user
    /// is, what the tenant's external registration settings still ask for, and - when an account
    /// with the same email exists - that account's user name to link to.
    /// </summary>
    [HttpGet("/api/crest/account/external-login")]
    [AllowAnonymous]
    public async Task<IActionResult> PendingExternalLogin()
    {
        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            return NotFound();
        }

        var settings = await _siteService.GetSettingsAsync<ExternalRegistrationSettings>();
        var email = info.GetEmail();
        var existing = _identityOptions.User.RequireUniqueEmail && !string.IsNullOrWhiteSpace(email)
            ? await _userManager.FindByEmailAsync(email)
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

    [HttpPost("/api/crest/account/external-login/register", Name = RateLimitRouteNames.RegisterExternalLogin)]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(UserRateLimiterPolicyNames.UserRegistration)]
    public async Task<IActionResult> RegisterExternalLogin([FromBody] RegisterExternalLoginViewModel model, [FromQuery] string returnUrl = null)
    {
        var settings = await _siteService.GetSettingsAsync<ExternalRegistrationSettings>();
        if (settings.DisableNewRegistrations)
        {
            return BadRequest(ExternalResult(S["New registrations are disabled for this site."]));
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            _logger.LogWarning("Error loading external login info.");
            return NotFound();
        }

        model.NoPassword = settings.NoPassword;
        model.NoEmail = settings.NoEmail;
        model.NoUsername = settings.NoUsername;

        UpdateAndValidateEmail(model, info, settings.NoEmail);
        await UpdateAndValidateUserNameAsync(model, info, settings.NoUsername);
        await UpdateAndValidatePasswordAsync(model, settings.NoPassword);

        if (ModelState.IsValid)
        {
            var iUser = await _userService.RegisterAsync(
                new RegisterUserForm()
                {
                    UserName = model.UserName,
                    Email = model.Email,
                    Password = model.Password,
                }, ModelState.AddModelError);

            if (iUser is not null && ModelState.IsValid)
            {
                var identityResult = await _userManager.AddLoginAsync(iUser, new UserLoginInfo(info.LoginProvider, info.ProviderKey, info.ProviderDisplayName));

                if (identityResult.Succeeded)
                {
                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation(3, "User account linked to {LoginProvider} provider.", info.LoginProvider);
                    }

                    foreach (var loginFormEvent in _loginFormEvents)
                    {
                        var loginResult = await loginFormEvent.ValidatingLoginAsync(iUser);

                        if (loginResult != null)
                        {
                            return BadRequest(ExternalResult(loginResult));
                        }
                    }

                    // We have created/linked to the local user, so we must verify the login.
                    // If it does not succeed, the user is not allowed to login.
                    var loginSettings = await _siteService.GetSettingsAsync<LoginSettings>();
                    var signInResult = await ExternalSignInAsync(iUser, info, GetRememberMe(info, loginSettings));

                    if (signInResult.Succeeded)
                    {
                        return Ok(ExternalResult(await LoggedInActionResultAsync(iUser, returnUrl, info), succeeded: true));
                    }
                }

                AddErrorsToModelState(identityResult.Errors);
            }
        }

        return BadRequest(ExternalResult((IActionResult)null));
    }

    [HttpPost("/api/crest/account/external-login/link", Name = RateLimitRouteNames.LinkExternalLogin)]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(UserRateLimiterPolicyNames.PasswordAuthentication)]
    public async Task<IActionResult> LinkExternalLogin([FromBody] LinkExternalLoginViewModel model, [FromQuery] string returnUrl = null)
    {
        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            _logger.LogWarning("Error loading external login info.");
            return NotFound();
        }

        var user = await _userManager.FindByEmailAsync(info.GetEmail());
        if (user == null)
        {
            _logger.LogWarning("Suspicious login detected from external provider. {LoginProvider} with key [{ProviderKey}] for {Identity}",
                info.LoginProvider, info.ProviderKey, info.Principal?.Identity?.Name);

            return BadRequest(ExternalResult(S["Invalid login attempt."]));
        }

        await _loginFormEvents.InvokeAsync((e, model, modelState) => e.LoggingInAsync(user.UserName, (key, message) => modelState.AddModelError(key, message)), model, ModelState, _logger);
        if (ModelState.IsValid)
        {
            var signInResult = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
            if (!signInResult.Succeeded)
            {
                return BadRequest(ExternalResult(S["Invalid login attempt."]));
            }

            var identityResult = await _userManager.AddLoginAsync(user, new UserLoginInfo(info.LoginProvider, info.ProviderKey, info.ProviderDisplayName));
            if (identityResult.Succeeded)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(3, "User account linked to {LoginProvider} provider.", info.LoginProvider);
                }

                // We have created/linked to the local user, so we must verify the login. If it
                // does not succeed, the user is not allowed to login.
                var loginSettings = await _siteService.GetSettingsAsync<LoginSettings>();
                if ((await ExternalSignInAsync(user, info, GetRememberMe(info, loginSettings))).Succeeded)
                {
                    return Ok(ExternalResult(await LoggedInActionResultAsync(user, returnUrl, info), succeeded: true));
                }
            }

            AddErrorsToModelState(identityResult.Errors);
        }

        return BadRequest(ExternalResult((IActionResult)null));
    }

    // The JSON answer of a completion: the model state's errors, and where a redirecting result
    // (a sign-in's return URL, a login veto's redirect) sends the browser.
    private ExternalLoginResult ExternalResult(IActionResult result, bool succeeded = false) => new(
        succeeded,
        [.. ModelState.Values.SelectMany(state => state.Errors).Select(error => error.ErrorMessage).Where(message => !string.IsNullOrEmpty(message))],
        result switch
        {
            RedirectResult redirect => Url.Content(redirect.Url),
            LocalRedirectResult local => Url.Content(local.Url),
            RedirectToActionResult action => Url.Action(action.ActionName, action.ControllerName, action.RouteValues),
            _ => null,
        });

    private ExternalLoginResult ExternalResult(string error)
    {
        ModelState.AddModelError(string.Empty, error);
        return ExternalResult((IActionResult)null);
    }

    private async Task UpdateAndValidatePasswordAsync(RegisterExternalLoginViewModel model, bool noPassword)
    {
        if (noPassword)
        {
            model.Password = null;
            model.ConfirmPassword = null;

            return;
        }

        if (string.IsNullOrWhiteSpace(model.Password))
        {
            ModelState.AddModelError(nameof(model.Password), S["Password is required!"]);
        }
        else if (model.Password != model.ConfirmPassword)
        {
            ModelState.AddModelError(nameof(model.ConfirmPassword), S["Confirm Password do not match"]);
        }
        else if (_userManager.PasswordValidators != null)
        {
            var user = new User();

            foreach (var passwordValidator in _userManager.PasswordValidators)
            {
                var validationResult = await passwordValidator.ValidateAsync(_userManager, user, model.Password);

                if (validationResult.Succeeded)
                {
                    continue;
                }

                foreach (var error in validationResult.Errors)
                {
                    ModelState.AddModelError(nameof(model.Password), error.Description);
                }
            }
        }
    }

    private async Task UpdateAndValidateUserNameAsync(RegisterExternalLoginViewModel model, ExternalLoginInfo info, bool noUsername)
    {
        if (noUsername && string.IsNullOrWhiteSpace(model.UserName))
        {
            model.UserName = await GenerateUsernameAsync(info);
        }

        if (string.IsNullOrWhiteSpace(model.UserName))
        {
            ModelState.AddModelError(nameof(model.UserName), S["Username is required!"]);
        }
    }

    private void UpdateAndValidateEmail(RegisterExternalLoginViewModel model, ExternalLoginInfo info, bool noEmail)
    {
        if (noEmail && string.IsNullOrWhiteSpace(model.Email))
        {
            model.Email = info.GetEmail();
        }

        if (string.IsNullOrWhiteSpace(model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), S["Email is required!"]);
        }
        else if (!_emailAddressValidator.Validate(model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), S["Invalid Email."]);
        }
    }

    private async Task<Microsoft.AspNetCore.Identity.SignInResult> ExternalSignInAsync(IUser user, ExternalLoginInfo info, bool rememberMe = false)
    {
        _logger.LogInformation("Attempting to do an external sign in.");

        var externalClaims = info.Principal.GetSerializableClaims().ToArray();
        var userRoles = await _userManager.GetRolesAsync(user);
        var userInfo = user as User;

        var context = new UpdateUserContext(user, info.LoginProvider, externalClaims, userInfo.Properties.DeepClone() as JsonObject)
        {
            UserClaims = userInfo.UserClaims,
            UserRoles = userRoles,
        };

        foreach (var externalLoginEventHandler in _externalLoginEventHandlers)
        {
            try
            {
                await externalLoginEventHandler.UpdateUserAsync(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The method {ExternalLoginHandler}.UpdateUserAsync(context) threw an exception", externalLoginEventHandler.GetType());
            }
        }

        if (await _userManager.UpdateUserPropertiesAsync(userInfo, context))
        {
            await _userManager.UpdateAsync(user);
        }

        var loginSettings = await _siteService.GetSettingsAsync<LoginSettings>();
        var isPersistent = loginSettings.AllowRememberMe
            ? rememberMe
            : loginSettings.UsePersistentAuthenticationCookie;

        var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent, bypassTwoFactor: true);

        if (result.Succeeded)
        {
            await _loginFormEvents.InvokeAsync((e, user) => e.LoggedInAsync(user), user, _logger);

            var identityResult = await _signInManager.UpdateExternalAuthenticationTokensAsync(info);

            if (!identityResult.Succeeded)
            {
                _logger.LogError("Error updating the external authentication tokens.");

                AddErrorsToModelState(identityResult.Errors);
            }
        }
        else
        {
            await _loginFormEvents.InvokeAsync((e, user) => e.LoggingInFailedAsync(user), user, _logger);
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

    private void AddErrorsToModelState(IEnumerable<IdentityError> errors)
    {
        foreach (var error in errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }

    private RedirectToActionResult RedirectToLogin(string returnUrl)
    {
        CopyModelStateErrorsToTempData();

        return RedirectToAction(nameof(AccountController.Login), typeof(AccountController).ControllerName(), new { returnUrl, externalLoginError = true });
    }

    private RedirectToActionResult RedirectToLogin()
        => RedirectToAction(nameof(AccountController.Login), typeof(AccountController).ControllerName());

    private void CopyModelStateErrorsToTempData(string key = "")
    {
        var iix = 0;

        foreach (var state in ModelState)
        {
            if (key != null && state.Key != key)
            {
                continue;
            }

            foreach (var item in state.Value.Errors)
            {
                TempData[$"error_{iix++}"] = item.ErrorMessage;
            }
        }
    }

    private async Task<string> GenerateUsernameAsync(ExternalLoginInfo info)
    {
        var ret = string.Concat("u", IdGenerator.GenerateId());
        var externalClaims = info?.Principal.GetSerializableClaims();
        var userNames = new Dictionary<Type, string>();

        foreach (var externalLoginEventHandler in _externalLoginEventHandlers)
        {
            try
            {
                var userName = await externalLoginEventHandler.GenerateUserName(info.LoginProvider, externalClaims.ToArray());
                if (!string.IsNullOrWhiteSpace(userName))
                {
                    // Set the return value to the username generated by the first IExternalLoginHandler.
                    if (userNames.Count == 0)
                    {
                        ret = userName;
                    }
                    userNames.Add(externalLoginEventHandler.GetType(), userName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{ExternalLoginHandler} - IExternalLoginHandler.GenerateUserName threw an exception", externalLoginEventHandler.GetType());
            }
        }

        if (userNames.Count > 1)
        {
            _logger.LogWarning("More than one IExternalLoginHandler generated username. Used first one registered, {ExternalLoginHandler}", userNames.FirstOrDefault().Key);
        }

        return ret;
    }
}
