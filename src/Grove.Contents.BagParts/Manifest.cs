using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Bag Parts",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Contents.BagParts",
    Name = "Bag Parts",
    Description = "Provides BagPart, a collection of content items held in their parent.",
    Category = "Content"
)]
