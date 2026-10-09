namespace Crest.AuditTrail.ViewModels;

public class AuditTrailSettingsViewModel
{
    public AuditTrailCategorySettingsViewModel[] Categories { get; set; } = [];
    public bool ClientIpAddressAllowed { get; set; }
}
