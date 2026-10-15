using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Queries",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Data.Queries.Core",
    Name = "Queries Core Services",
    Description = "Provides querying capability services.",
    Dependencies =
    ["Grove.Access", 
        "Grove.Display.Liquid",
    ],
    Category = "Content Management",
    EnabledByDependencyOnly = true
)]

[assembly: Feature(
    Id = "Grove.Data.Queries",
    Name = "Queries",
    Description = "Provides querying capabilities.",
    Dependencies =
    [
        "Grove.Data.Queries.Core",
    ],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Grove.Data.Queries.Sql",
    Name = "SQL Queries",
    Description = "Introduces a way to create custom Queries in pure SQL.",
    Dependencies =
    [
        "Grove.Data.Queries",
    ],
    Category = "Content Management"
)]
