using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Audit Trail",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Category = "Security"
)]

[assembly: Feature(
    Id = "Crest.AuditTrail",
    Name = "Audit Trail",
    Description = "Provides a log for recording and viewing back-end changes.",
    Category = "Security"
)]
