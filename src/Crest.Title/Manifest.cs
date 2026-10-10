using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Title",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "The title module enables content items to have titles.",
    Dependencies = ["Crest.Contents"],
    Category = "Content Management"
)]
