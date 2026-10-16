using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Elasticsearch",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Search.Elasticsearch",
    Name = "Elasticsearch",
    Description = "Creates Elasticsearch indexes to support search scenarios, on a connection to the cluster (Connections).",
    Dependencies =
    ["Grove.Access",
        "Grove.Data.Connectors",
        "Grove.Data.Queries.Core",
        "Grove.Data.Indexing",
        "Grove.Contents.Types",
    ],
    Category = "Search"
)]


[assembly: Feature(
    Id = "Grove.Search.Elasticsearch.Worker",
    Name = "Elasticsearch Worker",
    Description = "Provides a background task to keep indices in sync with other instances.",
    Dependencies = ["Grove.Search.Elasticsearch"],
    Category = "Search"
)]

