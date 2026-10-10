using Crest.Modules.Manifest;

[assembly: Module(
    Name = "ClamAV Antivirus Scanner",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.Antivirus.ClamAV",
    Name = "ClamAV Antivirus Scanner",
    Description = "Scans files with ClamAV before Crest stores them.",
    Category = "Security"
)]
