using System.ComponentModel;

namespace Grove.Contents.AuditTrail.Settings;

public class AuditTrailPartSettings
{
    [DefaultValue(true)]
    public bool ShowCommentInput { get; set; } = true;
}
