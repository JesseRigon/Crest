namespace Grove.Users;

// The route names the rate limits are keyed by (RateLimitsOptions.AddRouteRateLimit): the
// endpoints carry them as their names.
internal static class RateLimitRouteNames
{
    internal const string LinkExternalLogin = "LinkExternalLogin";

    internal const string Login = "Login";

    internal const string LoginWithRecoveryCode = "LoginWithRecoveryCode";

    internal const string LoginWithTwoFactorAuthentication = "LoginWithTwoFactorAuthentication";

    internal const string TwoFactorSendCode = "TwoFactorSendCode";

    internal const string Register = "Register";

    internal const string RegisterExternalLogin = "RegisterExternalLogin";

    internal const string ResetPassword = "ResetPassword";

    internal const string ForgotPassword = "ForgotPassword";
}
