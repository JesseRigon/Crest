using Grove.Security.Permissions;

namespace Grove.Contents;

public static class ContentTypesPermissions
{
    public static readonly Permission ViewContentTypes = new("ViewContentTypes", "View content types.");

    public static readonly Permission EditContentTypes = new("EditContentTypes", "Edit content types.", isSecurityCritical: true);
}
