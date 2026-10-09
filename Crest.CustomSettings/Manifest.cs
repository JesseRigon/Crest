using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Custom Settings",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "The custom settings modules enables content types to become custom site settings.",
    Dependencies = ["Crest.Contents"],
    Category = "Settings"
)]
