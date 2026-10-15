using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Placements",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Display.Placements",
    Name = "Placements",
    Description = "The Placements module provides a way to define shape placement in admin UI.",
    Category = "Development"
)]

[assembly: Feature(
    Id = "Grove.Display.Placements.FileStorage",
    Name = "Placements file storage",
    Description = "Stores Placements in a local file.",
    Dependencies = ["Grove.Display.Placements"],
    Category = "Development"
)]
