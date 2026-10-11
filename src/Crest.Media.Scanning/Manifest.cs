using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Media Scanning",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

// Every upload in every tenant is scanned before it is usable; no tenant runs without it
// (docs/media.md § 2, requirement 86).
[assembly: Feature(
    Id = "Crest.Media.Scanning",
    Name = "Upload Scanning",
    Description = "Quarantines every upload until host-level scanners and type detection pass it.",
    Category = "Security",
    IsAlwaysEnabled = true
)]
