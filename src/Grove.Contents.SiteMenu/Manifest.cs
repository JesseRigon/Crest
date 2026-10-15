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
        "Grove.Contents.Title",
        "Grove.Contents.Alias",
        "Grove.Infrastructure.Recipes.Core",
        // A content menu item picks its target through the option picker's content items source.
        "Grove.Contents.OptionPicker",
    ],
    Category = "Navigation"
)]
