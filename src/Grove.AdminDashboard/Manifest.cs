using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Admin Dashboard",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "Allows to organize widgets in an Admin Dashboard.",
    Dependencies =
    [
        "Grove.Admin",
        "Grove.Html",
        "Grove.Title",
        "Grove.Recipes.Core",
    ],
    Category = "Content Management"
)]
