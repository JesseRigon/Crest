using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Flows",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.Flows",
    Name = "Flows",
    Description = "Provides BagPart, a collection of content items held in their parent.",
    Category = "Content"
)]
