using Crest.Security.Permissions;

namespace Crest.Facebook;

public static class FacebookConstants
{
    public static readonly Permission ManageFacebookPixelPermission
        = new("ManageFacebookPixel", "Manage Facebook Pixel settings.");

    public const string PixelSettingsGroupId = "facebook-pixel";

    public const string ConversionsApiProtectorName = "Crest.Facebook.ConversionsApi";

    public static class Features
    {
        public const string Widgets = "Crest.Facebook.Widgets";
        public const string Login = "Crest.Facebook.Login";
        public const string Core = "Crest.Facebook";
        public const string Pixel = "Crest.Facebook.Pixel";
    }
}
