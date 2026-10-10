using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Crest Workflows UI",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.Workflows.UI",
    Name = "UI Activities",
    Description = "Provides UI related activities.",
    Category = "Crest.Workflows",
    Dependencies = ["Crest.Workflows"]
)]