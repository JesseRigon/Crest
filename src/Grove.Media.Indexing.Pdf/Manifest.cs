using Grove.Modules.Manifest;

[assembly: Module(
    Name = "PDF Media Indexing",
    Description = "Provides a way to index PDF files in search providers.",
    Dependencies =
    [
        "Grove.Media.Indexing"
    ],
    Category = "Search",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]
