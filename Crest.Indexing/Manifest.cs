using Crest.Indexing.Core;
using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Indexing",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Name = "Indexing",
    Id = IndexingConstants.Feature.Area,
    Description = "Provides index management.",
    Category = "Indexing"
)]

[assembly: Feature(
    Name = "Indexing Worker",
    Id = IndexingConstants.Feature.Worker,
    Description = "Provides a background task to keep indexes in sync with the latest content item update",
    Category = "Indexing",
    Dependencies = [IndexingConstants.Feature.Area, "Crest.Contents"]
)]
