using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Taxonomies",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.Taxonomies",
    Name = "Taxonomies",
    Description = "The taxonomies module provides a way to categorize content items.",
    Dependencies = ["Crest.ContentTypes"],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Crest.Taxonomies.ContentsAdminList",
    Name = "Taxonomies Contents List Filters",
    Description = "Provides taxonomy filters in the contents list.",
    Dependencies = ["Crest.Taxonomies"],
    Category = "Content Management"
)]
