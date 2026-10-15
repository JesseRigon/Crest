using Grove.Security.Permissions;

namespace Grove.Integrations.Facebook;

public static class FacebookConstants
{
    public static readonly Permission ManageFacebookPixelPermission
        = new("ManageFacebookPixel", "Manage Facebook Pixel settings.");

    public const string PixelSettingsGroupId = "facebook-pixel";

    public const string ConversionsApiProtectorName = "Grove.Integrations.Facebook.ConversionsApi";

    public static class Features
    {
        public const string Login = "Grove.Integrations.Facebook.Login";
        public const string Core = "Grove.Integrations.Facebook";
        public const string Pixel = "Grove.Integrations.Facebook.Pixel";
    }
}
