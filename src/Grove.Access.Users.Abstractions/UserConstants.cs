namespace Grove.Access.Users;

public static class UserConstants
{
    public const string TwoFactorAuthenticationClaimType = "TwoFacAuth";

    public static class Features
    {
        public const string Users = "Grove.Access.Users";

        public const string TwoFactorAuthentication = "Grove.Access.Users.2FA";

        public const string AuthenticatorApp = "Grove.Access.Users.2FA.AuthenticatorApp";

        public const string EmailAuthenticator = "Grove.Access.Users.2FA.Email";

        public const string SmsAuthenticator = "Grove.Access.Users.2FA.Sms";

        public const string UserRegistration = "Grove.Access.Users.Registration";

        public const string ExternalAuthentication = "Grove.Access.Users.ExternalAuthentication";

        public const string ResetPassword = "Grove.Access.Users.ResetPassword";
    }
}
