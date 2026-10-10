using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Rules",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.Rules",
    Name = "Rules",
    Description = "The Rules module adds rule building capabilities.",
    Dependencies =
    [
        "Crest.Scripting"
    ],
    Category = "Infrastructure"
)]
