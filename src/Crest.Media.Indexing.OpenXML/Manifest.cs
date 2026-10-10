using Crest.Modules.Manifest;

[assembly: Module(
    Name = "OpenXML Media Indexing",
    Description = "Provides a way to index Office files such as Word and Power Point in search providers",
    Dependencies =
    [
        "Crest.Media.Indexing"
    ],
    Category = "Search",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]
