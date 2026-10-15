using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Menu",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "The Menu module provides menu management features.",
    Dependencies =
    [
        "Grove.Contents",
        "Grove.Title",
        "Grove.Alias",
        "Grove.Recipes.Core",
    ],
    Category = "Navigation"
)]
