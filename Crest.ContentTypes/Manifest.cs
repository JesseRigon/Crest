using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Content Types",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "Content Types modules enables the creation and alteration of content types not based on code.",
    Dependencies = ["Crest.Contents"],
    Category = "Content Management"
)]
