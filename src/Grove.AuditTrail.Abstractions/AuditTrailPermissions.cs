using Grove.Security.Permissions;

namespace Grove.AuditTrail;

public static class AuditTrailPermissions
{
    public static readonly Permission ViewAuditTrail = new(nameof(ViewAuditTrail), "View audit trail");

    public static readonly Permission ManageAuditTrailSettings = new(nameof(ManageAuditTrailSettings), "Manage audit trail settings");
}
