using Grove.Modules.Manifest;

[assembly: Module(
    Name = "SEO",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "Provides SEO meta features",
    Category = "Content Management",
    Dependencies =
    [
        "Grove.Contents",
        "Grove.Infrastructure.Recipes.Core",
        "Grove.Media",
        "Grove.Shortcodes",
    ]
)]
