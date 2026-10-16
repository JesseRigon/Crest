using System.ComponentModel;

namespace Grove.Contents.AuditTrail.Settings;

public class AuditTrailFieldSetSettings
{
    [DefaultValue(true)]
    public bool ShowCommentInput { get; set; } = true;
}
