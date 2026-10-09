using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Queries",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.Queries.Core",
    Name = "Queries Core Services",
    Description = "Provides querying capability services.",
    Dependencies =
    [
        "Crest.Liquid",
    ],
    Category = "Content Management",
    EnabledByDependencyOnly = true
)]

[assembly: Feature(
    Id = "Crest.Queries",
    Name = "Queries",
    Description = "Provides querying capabilities.",
    Dependencies =
    [
        "Crest.Queries.Core",
    ],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Crest.Queries.Sql",
    Name = "SQL Queries",
    Description = "Introduces a way to create custom Queries in pure SQL.",
    Dependencies =
    [
        "Crest.Queries",
    ],
    Category = "Content Management"
)]
