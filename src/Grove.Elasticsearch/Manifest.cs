using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Elasticsearch",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Elasticsearch",
    Name = "Elasticsearch",
    Description = "Creates Elasticsearch indexes to support search scenarios, introduces a preconfigured container-enabled content type.",
    Dependencies =
    ["Grove.Access", 
        "Grove.Queries.Core",
        "Grove.Indexing",
        "Grove.ContentTypes",
    ],
    Category = "Search"
)]

[assembly: Feature(
    Id = "Grove.Search.Elasticsearch",
    Name = "Elasticsearch (Obsolete)",
    Description = "Obsolete legacy feature ID kept for backwards compatibility. Enables Grove.Elasticsearch automatically.",
    Dependencies = ["Grove.Elasticsearch"],
    Category = "Search"
)]

[assembly: Feature(
    Id = "Grove.Search.Elasticsearch.Worker",
    Name = "Elasticsearch Worker",
    Description = "Provides a background task to keep indices in sync with other instances.",
    Dependencies = ["Grove.Search.Elasticsearch"],
    Category = "Search"
)]

[assembly: Feature(
    Id = "Grove.Search.Elasticsearch.ContentPicker",
    Name = "Elasticsearch Content Picker",
    Description = "Provides a Elasticsearch content picker field editor.",
    Dependencies = ["Grove.Search.Elasticsearch", "Grove.ContentFields"],
    Category = "Search"
)]
