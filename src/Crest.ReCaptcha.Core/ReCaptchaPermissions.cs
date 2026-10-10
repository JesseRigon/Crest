using Crest.Security.Permissions;

namespace Crest.ReCaptcha;

public static class ReCaptchaPermissions
{
    public static readonly Permission ManageReCaptchaSettings = new("ManageReCaptchaSettings", "Manage ReCaptcha Settings");
}
