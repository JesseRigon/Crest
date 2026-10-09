using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Admin",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "Creates an admin section for the site.",
    Category = "Infrastructure",
    Dependencies =
    [
        "Crest.Settings"
    ]
)]
