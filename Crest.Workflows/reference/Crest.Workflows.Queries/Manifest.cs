using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Crest Workflows Queries",
    Author = ManifestConstants.OrchardCoreTeam,
    Website = ManifestConstants.OrchardCoreWebsite,
    Version = ManifestConstants.OrchardCoreVersion
)]

[assembly: Feature(
    Id = "Crest.Workflows.Queries",
    Name = "Query Activities",
    Description = "Provides query related activities.",
    Category = "Crest.Workflows",
    Dependencies = ["Crest.Workflows", "OrchardCore.Queries.Sql"]
)]