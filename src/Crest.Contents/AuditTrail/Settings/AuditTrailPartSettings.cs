using System.ComponentModel;

namespace Crest.Contents.AuditTrail.Settings;

public class AuditTrailPartSettings
{
    [DefaultValue(true)]
    public bool ShowCommentInput { get; set; } = true;
}
