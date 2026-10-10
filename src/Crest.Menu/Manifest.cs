using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Menu",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "The Menu module provides menu management features.",
    Dependencies =
    [
        "Crest.Contents",
        "Crest.Title",
        "Crest.Alias",
        "Crest.Recipes.Core",
    ],
    Category = "Navigation"
)]
