using Crest.Modules.Manifest;

[assembly: Module(
    Name = "PDF Media Indexing",
    Description = "Provides a way to index PDF files in search providers.",
    Dependencies =
    [
        "Crest.Media.Indexing"
    ],
    Category = "Search",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]
