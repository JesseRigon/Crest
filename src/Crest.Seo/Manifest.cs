using Crest.Modules.Manifest;

[assembly: Module(
    Name = "SEO",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "Provides SEO meta features",
    Category = "Content Management",
    Dependencies =
    [
        "Crest.Contents",
        "Crest.Recipes.Core",
        "Crest.Media",
        "Crest.Shortcodes",
    ]
)]
