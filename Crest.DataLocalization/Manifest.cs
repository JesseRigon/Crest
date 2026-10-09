using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Data Localization",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "Provides support for data localization.",
    Category = "Internationalization",
    Dependencies = ["Crest.Localization"]
)]
