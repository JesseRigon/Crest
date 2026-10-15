using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Taxonomies",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Taxonomies",
    Name = "Taxonomies",
    Description = "The taxonomies module provides a way to categorize content items.",
    Dependencies = ["Grove.ContentTypes"],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Grove.Taxonomies.ContentsAdminList",
    Name = "Taxonomies Contents List Filters",
    Description = "Provides taxonomy filters in the contents list.",
    Dependencies = ["Grove.Taxonomies"],
    Category = "Content Management"
)]
