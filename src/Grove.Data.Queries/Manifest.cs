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
    Id = "Grove.Data.Queries.Connections",
    Name = "Connection Queries",
    Description = "Queries over the operations of connections (REST, GraphQL, SOAP) through the connector engine, as typed, paged rows.",
    Dependencies =
    [
        "Grove.Data.Queries",
        "Grove.Data.Connectors",
    ],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Grove.Data.Queries.Sql",
    Name = "SQL Queries",
    Description = "Custom Queries in pure SQL, over the tenant's own database (scoped to the caller) or an external database's SQL connection.",
    Dependencies =
    [
        "Grove.Data.Queries",
    ],
    Category = "Content Management"
)]
