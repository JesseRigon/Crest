using Grove.Security.Permissions;

namespace Grove.ReCaptcha;

public static class ReCaptchaPermissions
{
    public static readonly Permission ManageReCaptchaSettings = new("ManageReCaptchaSettings", "Manage ReCaptcha Settings");
}
