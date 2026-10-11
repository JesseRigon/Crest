using Crest.Security.Permissions;

namespace Crest.Sms;

public static class SmsPermissions
{
    public static readonly Permission ManageSmsSettings = new("ManageSmsSettings", "Manage SMS Settings");
}
