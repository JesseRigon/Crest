using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Audit Trail",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Category = "Security"
)]

[assembly: Feature(
    Id = "Grove.AuditTrail",
    Name = "Audit Trail",
    Description = "Provides a log for recording and viewing back-end changes.",
    Category = "Security",
    // After Settings: the audit trail decorates the site service to record settings writes.
    Dependencies = ["Grove.Settings"]
)]
