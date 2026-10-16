using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Categories",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Contents.Taxonomies",
    Name = "Categories",
    Description = "Categories (taxonomies) to file content items under: tag lists and category trees.",
    Dependencies = ["Grove.Contents.Types", "Grove.Contents.OptionPicker"],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Grove.Contents.Taxonomies.ContentsAdminList",
    Name = "Category Filters",
    Description = "Provides category filters in the contents list.",
    Dependencies = ["Grove.Contents.Taxonomies"],
    Category = "Content Management"
)]
