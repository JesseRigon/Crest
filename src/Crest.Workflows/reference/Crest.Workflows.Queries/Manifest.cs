using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Crest Workflows Queries",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.Workflows.Queries",
    Name = "Query Activities",
    Description = "Provides query related activities.",
    Category = "Crest.Workflows",
    Dependencies = ["Crest.Workflows", "Crest.Queries.Sql"]
)]