using Grove.Security.Permissions;

namespace Grove.Facebook;

public static class FacebookConstants
{
    public static readonly Permission ManageFacebookPixelPermission
        = new("ManageFacebookPixel", "Manage Facebook Pixel settings.");

    public const string PixelSettingsGroupId = "facebook-pixel";

    public const string ConversionsApiProtectorName = "Grove.Facebook.ConversionsApi";

    public static class Features
    {
        public const string Login = "Grove.Facebook.Login";
        public const string Core = "Grove.Facebook";
        public const string Pixel = "Grove.Facebook.Pixel";
    }
}
