using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Settings",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "The settings module creates site settings that other modules can contribute to.",
    Category = "Configuration",
    IsAlwaysEnabled = true
)]
