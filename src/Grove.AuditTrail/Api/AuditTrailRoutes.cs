namespace Grove.AuditTrail.Api;

/// <summary>
/// The audit API and admin page routes. Dependency-free: the admin pages library
/// (blazor-wasm, Grove.AuditTrail.BlazorWasm) compiles this file too.
/// </summary>
public static class AuditTrailRoutes
{
    public const string Api = "api/grove/audit";

    /// <summary>The admin pages, relative to the admin path (Grove.AuditTrail.BlazorWasm).</summary>
    public const string TrailAdmin = "/Audit";

    public const string SessionsAdmin = "/Audit/Sessions";

    public const string SettingsAdmin = "/Audit/Settings";

    public const string RecordAdmin = "/Audit/Records/{RecordId}";
}
