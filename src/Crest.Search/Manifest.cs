using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Search",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.Search",
    Name = "Search",
    Description = "Provides frontend search capabilities against indexes.",
    Category = "Search",
    Dependencies =
    [
        "Crest.Indexing",
    ]
)]
