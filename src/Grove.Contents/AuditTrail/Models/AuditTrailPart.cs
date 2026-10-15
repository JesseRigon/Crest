using Grove.Contents.Management;

namespace Grove.Contents.AuditTrail.Models;

public class AuditTrailPart : ContentPart
{
    public string Comment { get; set; }
    public bool ShowComment { get; set; }
}
