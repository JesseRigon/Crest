using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Content Preview",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "The content Preview module enables live content edition and content preview.",
    Dependencies = ["Crest.Contents"],
    Category = "Content Management"
)]
