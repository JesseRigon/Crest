using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Placements",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.Placements",
    Name = "Placements",
    Description = "The Placements module provides a way to define shape placement in admin UI.",
    Category = "Development"
)]

[assembly: Feature(
    Id = "Crest.Placements.FileStorage",
    Name = "Placements file storage",
    Description = "Stores Placements in a local file.",
    Dependencies = ["Crest.Placements"],
    Category = "Development"
)]
