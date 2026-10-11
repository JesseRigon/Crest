namespace Crest.Users;

public static class UserConstants
{
    public const string TwoFactorAuthenticationClaimType = "TwoFacAuth";

    public static class Features
    {
        public const string Users = "Crest.Users";

        public const string TwoFactorAuthentication = "Crest.Users.2FA";

        public const string AuthenticatorApp = "Crest.Users.2FA.AuthenticatorApp";

        public const string EmailAuthenticator = "Crest.Users.2FA.Email";

        public const string SmsAuthenticator = "Crest.Users.2FA.Sms";

        public const string UserRegistration = "Crest.Users.Registration";

        public const string ExternalAuthentication = "Crest.Users.ExternalAuthentication";

        public const string ResetPassword = "Crest.Users.ResetPassword";
    }
}
