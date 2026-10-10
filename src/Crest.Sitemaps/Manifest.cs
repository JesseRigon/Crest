using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Sitemaps",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.Sitemaps",
    Name = "Sitemaps",
    Description = "Provides dynamic sitemap generation services.",
    Dependencies =
    [
        "Crest.Contents",
    ],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Crest.Sitemaps.RazorPages",
    Name = "Sitemaps for Decoupled Razor Pages",
    Description = "Provides decoupled razor pages support for dynamic sitemap generation.",
    Dependencies =
    [
        "Crest.Sitemaps"
    ],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Crest.Sitemaps.Cleanup",
    Name = "Sitemaps Cleanup",
    Description = "Cleanup sitemap cache files through a background task.",
    Dependencies =
    [
        "Crest.Sitemaps"
    ],
    Category = "Content Management"
)]
