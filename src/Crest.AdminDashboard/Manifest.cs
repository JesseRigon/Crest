using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Admin Dashboard",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "Allows to organize widgets in an Admin Dashboard.",
    Dependencies =
    [
        "Crest.Admin",
        "Crest.Html",
        "Crest.Title",
        "Crest.Recipes.Core",
    ],
    Category = "Content Management"
)]
