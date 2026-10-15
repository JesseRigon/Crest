namespace Grove.Users;

public static class UserConstants
{
    public const string TwoFactorAuthenticationClaimType = "TwoFacAuth";

    public static class Features
    {
        public const string Users = "Grove.Users";

        public const string TwoFactorAuthentication = "Grove.Users.2FA";

        public const string AuthenticatorApp = "Grove.Users.2FA.AuthenticatorApp";

        public const string EmailAuthenticator = "Grove.Users.2FA.Email";

        public const string SmsAuthenticator = "Grove.Users.2FA.Sms";

        public const string UserRegistration = "Grove.Users.Registration";

        public const string ExternalAuthentication = "Grove.Users.ExternalAuthentication";

        public const string ResetPassword = "Grove.Users.ResetPassword";
    }
}
