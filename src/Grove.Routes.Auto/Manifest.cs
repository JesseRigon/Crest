using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Autoroute",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "Provides a way to automatically generate routes for content items based on their content type and title.",
    Dependencies =
    [
        "Grove.Contents.Types",
        "Grove.HomeRoute",
    ],
    Category = "Navigation"
)]
