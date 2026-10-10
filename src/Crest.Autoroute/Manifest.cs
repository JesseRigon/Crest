using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Autoroute",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "Provides a way to automatically generate routes for content items based on their content type and title.",
    Dependencies =
    [
        "Crest.ContentTypes",
        "Crest.HomeRoute",
    ],
    Category = "Navigation"
)]
