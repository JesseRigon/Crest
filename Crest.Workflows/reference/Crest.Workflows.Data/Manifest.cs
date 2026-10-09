using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Crest Workflows Data",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.Workflows.Data.Csv",
    Name = "CSV Activities",
    Description = "Provides CSV related activities.",
    Category = "Crest.Workflows",
    Dependencies = ["Crest.Workflows"]
)]