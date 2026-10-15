#nullable enable

using Grove.Access;

namespace Grove.Users.Api;

public sealed record LoginRequest(string UserName, string Password, bool RememberMe);

/// <summary>
/// Who the caller is. A sign-in answers it too: <see cref="RequiresTwoFactor"/> when the password
/// step passed and the second step is pending, <see cref="Redirect"/> when the signed-in user must
/// go somewhere first (the two-factor page, for a user who must set it up).
/// </summary>
public sealed record AuthUser(bool IsAuthenticated, string? UserName, string[] Roles, bool RequiresTwoFactor = false, string? Redirect = null);

/// <summary>A sign-in form's shape (<c>api/grove/auth/form/{form}</c>).</summary>
public sealed record AccessFormModel(bool LocalLogin, bool AllowRememberMe, bool RememberMe, LoginExtension[] Extensions, string? Redirect);

/// <summary>The pending second step: the account's methods and the one to ask for first.</summary>
public sealed record TwoFactorChallengeModel(string[] Providers, string? Provider, bool AllowRememberDevice);

public sealed record TwoFactorSignInRequest(string? Provider, string? Code, bool RememberMe, bool RememberDevice);

public sealed record RecoveryCodeSignInRequest(string? Code);

public sealed record TwoFactorSendCodeRequest(string? Provider);

public sealed record TwoFactorCodeSent(bool Sent, string Message);
