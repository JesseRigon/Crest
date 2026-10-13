namespace Crest.Contents.AuditTrail.Settings;

public class ContentAuditTrailSettings
{
    /// <summary>
    /// The content types whose events are not recorded. Every other type is (docs/audit.md ›
    /// step 1: every content type, Crest's and downstream modules', is audited by default).
    /// </summary>
    public string[] ExcludedContentTypes { get; set; } = [];
}
