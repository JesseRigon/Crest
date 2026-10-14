namespace Crest.AuditTrail.Api;

/// <summary>
/// The audit API and admin page routes. Dependency-free: the admin pages library
/// (blazor-wasm, Crest.AuditTrail.BlazorWasm) compiles this file too.
/// </summary>
public static class AuditTrailRoutes
{
    public const string Api = "api/crest/audit";

    /// <summary>The admin pages, relative to the admin path (Crest.AuditTrail.BlazorWasm).</summary>
    public const string TrailAdmin = "/Audit";

    public const string SessionsAdmin = "/Audit/Sessions";

    public const string SettingsAdmin = "/Audit/Settings";

    public const string RecordAdmin = "/Audit/Records/{RecordId}";
}
