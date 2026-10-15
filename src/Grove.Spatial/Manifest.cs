using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Spatial",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "This feature provides the ability to provide spatial locations to content items.",
    Dependencies = ["Grove.ContentTypes", "Grove.Lucene"],
    Category = "Content Management"
)]
