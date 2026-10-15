using Grove.Modules.Manifest;

[assembly: Module(
    Name = "ClamAV Antivirus Scanner",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Security.Antivirus.ClamAV",
    Name = "ClamAV Antivirus Scanner",
    Description = "Scans files with ClamAV before Grove stores them.",
    Category = "Security"
)]
