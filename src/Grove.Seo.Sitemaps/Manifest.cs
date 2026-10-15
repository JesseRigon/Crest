using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Sitemaps",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Seo.Sitemaps",
    Name = "Sitemaps",
    Description = "Provides dynamic sitemap generation services.",
    Dependencies =
    [
        "Grove.Contents",
    ],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Grove.Seo.Sitemaps.Cleanup",
    Name = "Sitemaps Cleanup",
    Description = "Cleanup sitemap cache files through a background task.",
    Dependencies =
    [
        "Grove.Seo.Sitemaps"
    ],
    Category = "Content Management"
)]
