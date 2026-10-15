using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Title",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "The title module enables content items to have titles.",
    Dependencies = ["Grove.Contents"],
    Category = "Content Management"
)]
