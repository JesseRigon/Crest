using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Forms",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.Forms",
    Name = "Forms",
    Description = "Provides widgets and activities to implement forms.",
    Dependencies = ["Crest.Widgets", "Crest.Flows"],
    Category = "Content"
)]
