using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Infrastructure.Settings;
using Grove.Tests.Apis.Context;
using Grove.Access.Users;
using Grove.Access.Users.Models;
using Microsoft.Net.Http.Headers;

namespace Grove.Tests.Modules.Grove.Users;

/// <summary>
/// The Access APIs behind the sign-in, registration, password recovery and two-factor pages
/// (Grove.Access.Users' AuthApi, RegistrationApi, PasswordResetApi, TwoFactorApi) and the access gate's
/// rule for a user who must set up two-factor authentication first.
/// </summary>
public class AccessApiTests
{
    private const string AdminUserName = "admin";
    private const string AdminPassword = "Password01_";

    [Fact]
    public async Task SignIn_WithoutTwoFactor_SignsIn()
    {
        var context = await GetSiteContextAsync([]);
        var session = new Session(context.Client);

        var response = await session.PostAsync("api/grove/auth/login", new { userName = AdminUserName, password = AdminPassword, rememberMe = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var answer = await response.Content.ReadFromJsonAsync<JsonObject>(TestContext.Current.CancellationToken);
        Assert.True(answer!["isAuthenticated"]!.GetValue<bool>());
        Assert.False(answer["requiresTwoFactor"]!.GetValue<bool>());
        Assert.Contains("orchauth_" + context.TenantName, session.Cookies.Keys);
    }

    [Fact]
    public async Task SignIn_WrongPassword_IsRefusedWithAReason()
    {
        var context = await GetSiteContextAsync([]);
        var session = new Session(context.Client);

        var response = await session.PostAsync("api/grove/auth/login", new { userName = AdminUserName, password = "wrong", rememberMe = false });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var refusal = await response.Content.ReadFromJsonAsync<JsonObject>(TestContext.Current.CancellationToken);
        Assert.NotEmpty(refusal!["errors"]!.AsArray());
        Assert.DoesNotContain("orchauth_" + context.TenantName, session.Cookies.Keys);
    }

    [Fact]
    public async Task SignIn_WithTwoFactor_AsksForTheCodeThenSignsIn()
    {
        var context = await GetSiteContextAsync([UserConstants.Features.TwoFactorAuthentication, UserConstants.Features.EmailAuthenticator]);
        await EnableEmailTwoFactorAsync(context);
        var session = new Session(context.Client);

        // The password step: not signed in, the second step pending.
        var login = await session.PostAsync("api/grove/auth/login", new { userName = AdminUserName, password = AdminPassword, rememberMe = false });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var answer = await login.Content.ReadFromJsonAsync<JsonObject>(TestContext.Current.CancellationToken);
        Assert.False(answer!["isAuthenticated"]!.GetValue<bool>());
        Assert.True(answer["requiresTwoFactor"]!.GetValue<bool>());
        Assert.DoesNotContain("orchauth_" + context.TenantName, session.Cookies.Keys);

        // The challenge names the account's method.
        var challenge = await session.GetAsync("api/grove/auth/two-factor");
        Assert.Equal(HttpStatusCode.OK, challenge.StatusCode);
        var state = await challenge.Content.ReadFromJsonAsync<JsonObject>(TestContext.Current.CancellationToken);
        Assert.Contains("Email", state!["providers"]!.AsArray().Select(provider => provider!.GetValue<string>()));

        // A wrong code is refused; the account's code signs in.
        var wrong = await session.PostAsync("api/grove/auth/two-factor", new { provider = "Email", code = "000000", rememberMe = false, rememberDevice = false });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

        var code = await GenerateEmailCodeAsync(context);
        var signedIn = await session.PostAsync("api/grove/auth/two-factor", new { provider = "Email", code, rememberMe = false, rememberDevice = false });
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        var user = await signedIn.Content.ReadFromJsonAsync<JsonObject>(TestContext.Current.CancellationToken);
        Assert.True(user!["isAuthenticated"]!.GetValue<bool>());
        Assert.Contains("orchauth_" + context.TenantName, session.Cookies.Keys);
    }

    [Fact]
    public async Task SignIn_WithTwoFactor_RecoveryCodeSignsInOnce()
    {
        var context = await GetSiteContextAsync([UserConstants.Features.TwoFactorAuthentication, UserConstants.Features.EmailAuthenticator]);
        await EnableEmailTwoFactorAsync(context);
        string recoveryCode = null;
        await context.UsingTenantScopeAsync(async scope =>
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IUser>>();
            var user = await userManager.FindByNameAsync(AdminUserName);
            recoveryCode = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 3)).First();
        });

        var session = new Session(context.Client);
        await session.PostAsync("api/grove/auth/login", new { userName = AdminUserName, password = AdminPassword, rememberMe = false });
        var signedIn = await session.PostAsync("api/grove/auth/two-factor/recovery", new { code = recoveryCode });
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);

        // The code is spent.
        var again = new Session(context.Client);
        await again.PostAsync("api/grove/auth/login", new { userName = AdminUserName, password = AdminPassword, rememberMe = false });
        var reused = await again.PostAsync("api/grove/auth/two-factor/recovery", new { code = recoveryCode });
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
    }

    [Fact]
    public async Task Registration_CreatesTheAccountAndSignsItIn()
    {
        var context = await GetSiteContextAsync([UserConstants.Features.UserRegistration]);
        var session = new Session(context.Client);

        var response = await session.PostAsync("api/grove/registration", new { userName = "newuser", email = "newuser@grove.test", password = "Password01_", confirmPassword = "Password01_" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonObject>(TestContext.Current.CancellationToken);
        Assert.True(result!["succeeded"]!.GetValue<bool>());
        Assert.Contains("orchauth_" + context.TenantName, session.Cookies.Keys);
        await context.UsingTenantScopeAsync(async scope =>
            Assert.NotNull(await scope.ServiceProvider.GetRequiredService<UserManager<IUser>>().FindByNameAsync("newuser")));
    }

    [Fact]
    public async Task Registration_RefusesAMismatchedConfirmation()
    {
        var context = await GetSiteContextAsync([UserConstants.Features.UserRegistration]);
        var session = new Session(context.Client);

        var response = await session.PostAsync("api/grove/registration", new { userName = "newuser", email = "newuser@grove.test", password = "Password01_", confirmPassword = "Other01_" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await context.UsingTenantScopeAsync(async scope =>
            Assert.Null(await scope.ServiceProvider.GetRequiredService<UserManager<IUser>>().FindByNameAsync("newuser")));
    }

    [Fact]
    public async Task Registration_UnderModeration_GoesToRegistrationPending()
    {
        var context = await GetSiteContextAsync([UserConstants.Features.UserRegistration], new RegistrationSettings { UsersAreModerated = true });
        var session = new Session(context.Client);

        var response = await session.PostAsync("api/grove/registration", new { userName = "pending", email = "pending@grove.test", password = "Password01_", confirmPassword = "Password01_" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonObject>(TestContext.Current.CancellationToken);
        Assert.EndsWith("/RegistrationPending", result!["redirect"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ForgotPassword_AnswersTheSameWhetherOrNotTheAccountExists()
    {
        var context = await GetSiteContextAsync([UserConstants.Features.ResetPassword], resetPassword: new ResetPasswordSettings { AllowResetPassword = true });

        var known = await new Session(context.Client).PostAsync("api/grove/password/forgot", new { userNameOrEmail = AdminUserName });
        var unknown = await new Session(context.Client).PostAsync("api/grove/password/forgot", new { userNameOrEmail = "nobody" });

        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), await unknown.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ResetPassword_WithTheLinksCode_SetsTheNewPassword()
    {
        var context = await GetSiteContextAsync([UserConstants.Features.ResetPassword], resetPassword: new ResetPasswordSettings { AllowResetPassword = true });
        string code = null;
        await context.UsingTenantScopeAsync(async scope =>
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IUser>>();
            var token = await userManager.GeneratePasswordResetTokenAsync(await userManager.FindByNameAsync(AdminUserName));
            code = Convert.ToBase64String(Encoding.UTF8.GetBytes(token));
        });

        var reset = await new Session(context.Client).PostAsync("api/grove/password/reset", new { userNameOrEmail = AdminUserName, code, newPassword = "NewPassword02_", confirmPassword = "NewPassword02_" });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        var login = await new Session(context.Client).PostAsync("api/grove/auth/login", new { userName = AdminUserName, password = "NewPassword02_", rememberMe = false });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WithAnInvalidCode_IsRefused()
    {
        var context = await GetSiteContextAsync([UserConstants.Features.ResetPassword], resetPassword: new ResetPasswordSettings { AllowResetPassword = true });

        var reset = await new Session(context.Client).PostAsync("api/grove/password/reset", new { userNameOrEmail = AdminUserName, code = Convert.ToBase64String(Encoding.UTF8.GetBytes("forged")), newPassword = "NewPassword02_", confirmPassword = "NewPassword02_" });

        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
    }

    [Fact]
    public async Task TwoFactorSetupRequired_TheSessionReachesOnlyTheSetup()
    {
        var context = await GetSiteContextAsync(
            [UserConstants.Features.TwoFactorAuthentication, UserConstants.Features.EmailAuthenticator],
            twoFactor: new TwoFactorLoginSettings { RequireTwoFactorAuthentication = true, NumberOfRecoveryCodesToGenerate = 5 });
        var session = new Session(context.Client);

        // Signed in, and sent to the two-factor page first.
        var login = await session.PostAsync("api/grove/auth/login", new { userName = AdminUserName, password = AdminPassword, rememberMe = false });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var answer = await login.Content.ReadFromJsonAsync<JsonObject>(TestContext.Current.CancellationToken);
        Assert.EndsWith("/TwoFactor", answer!["redirect"]!.GetValue<string>(), StringComparison.Ordinal);

        // The setup's API answers.
        var setup = await session.GetAsync("api/grove/two-factor");
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);

        // Any other API is refused with the reason, never redirected.
        var other = await session.GetAsync("api/grove/users");
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
        var problem = await other.Content.ReadFromJsonAsync<JsonObject>(TestContext.Current.CancellationToken);
        Assert.Equal("two-factor-setup-required", problem!["reason"]!.GetValue<string>());

        // A page goes to the setup.
        var page = await session.GetAsync(string.Empty);
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.EndsWith("/TwoFactor", page.Headers.Location!.ToString(), StringComparison.Ordinal);

        // Signing out still works.
        var logout = await session.PostAsync("api/grove/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
    }

    // The admin account with the email method set up: its email confirmed, two-factor on.
    private static Task EnableEmailTwoFactorAsync(SiteContext context) =>
        context.UsingTenantScopeAsync(async scope =>
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IUser>>();
            var user = (User)await userManager.FindByNameAsync(AdminUserName);
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
            await userManager.SetTwoFactorEnabledAsync(user, true);
        });

    private static async Task<string> GenerateEmailCodeAsync(SiteContext context)
    {
        string code = null;
        await context.UsingTenantScopeAsync(async scope =>
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IUser>>();
            code = await userManager.GenerateTwoFactorTokenAsync(await userManager.FindByNameAsync(AdminUserName), TokenOptions.DefaultEmailProvider);
        });

        return code;
    }

    private static async Task<SiteContext> GetSiteContextAsync(
        string[] features,
        RegistrationSettings registration = null,
        ResetPasswordSettings resetPassword = null,
        TwoFactorLoginSettings twoFactor = null)
    {
        var context = new SiteContext();
        await context.InitializeAsync();

        var settings = new JsonObject { ["name"] = "settings" };
        if (registration is not null)
        {
            settings[nameof(RegistrationSettings)] = JsonSerializer.SerializeToNode(registration);
        }

        if (resetPassword is not null)
        {
            settings[nameof(ResetPasswordSettings)] = JsonSerializer.SerializeToNode(resetPassword);
        }

        if (twoFactor is not null)
        {
            settings[nameof(TwoFactorLoginSettings)] = JsonSerializer.SerializeToNode(twoFactor);
        }

        var steps = new JsonArray();
        if (settings.Count > 1)
        {
            steps.Add(settings);
        }

        if (features.Length > 0)
        {
            steps.Add(new JsonObject
            {
                ["name"] = "feature",
                ["enable"] = new JsonArray([.. features.Select(feature => (JsonNode)feature)]),
            });
        }

        await RecipeHelpers.RunRecipeAsync(context, new JsonObject { ["steps"] = steps });

        return context;
    }

    /// <summary>A browser's cookies across calls, on the admin side (X-Shell), as the shells call the APIs.</summary>
    private sealed class Session(HttpClient client)
    {
        public Dictionary<string, string> Cookies { get; } = [];

        public Task<HttpResponseMessage> GetAsync(string path) => SendAsync(new HttpRequestMessage(HttpMethod.Get, path));

        public Task<HttpResponseMessage> PostAsync(string path, object body) =>
            SendAsync(new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body ?? new { }) });

        private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            request.Headers.TryAddWithoutValidation("X-Shell", "admin");
            CookiesHelper.AddCookiesToRequest(request, Cookies);

            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            if (response.Headers.TryGetValues("Set-Cookie", out var values))
            {
                foreach (var cookie in SetCookieHeaderValue.ParseList(values.ToList()))
                {
                    // A cookie set in the past is a deletion (a sign-out, the spent two-factor cookie).
                    if (cookie.Expires is { } expires && expires < DateTimeOffset.UtcNow)
                    {
                        Cookies.Remove(cookie.Name.ToString());
                    }
                    else
                    {
                        Cookies[cookie.Name.ToString()] = cookie.Value.ToString();
                    }
                }
            }

            return response;
        }
    }
}
