using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Sitemaps",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Sitemaps",
    Name = "Sitemaps",
    Description = "Provides dynamic sitemap generation services.",
    Dependencies =
    [
        "Grove.Contents",
    ],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Grove.Sitemaps.RazorPages",
    Name = "Sitemaps for Decoupled Razor Pages",
    Description = "Provides decoupled razor pages support for dynamic sitemap generation.",
    Dependencies =
    [
        "Grove.Sitemaps"
    ],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Grove.Sitemaps.Cleanup",
    Name = "Sitemaps Cleanup",
    Description = "Cleanup sitemap cache files through a background task.",
    Dependencies =
    [
        "Grove.Sitemaps"
    ],
    Category = "Content Management"
)]
