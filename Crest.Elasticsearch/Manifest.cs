using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Elasticsearch",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.Elasticsearch",
    Name = "Elasticsearch",
    Description = "Creates Elasticsearch indexes to support search scenarios, introduces a preconfigured container-enabled content type.",
    Dependencies =
    [
        "Crest.Queries.Core",
        "Crest.Indexing",
        "Crest.ContentTypes",
    ],
    Category = "Search"
)]

[assembly: Feature(
    Id = "Crest.Search.Elasticsearch",
    Name = "Elasticsearch (Obsolete)",
    Description = "Obsolete legacy feature ID kept for backwards compatibility. Enables Crest.Elasticsearch automatically.",
    Dependencies = ["Crest.Elasticsearch"],
    Category = "Search"
)]

[assembly: Feature(
    Id = "Crest.Search.Elasticsearch.Worker",
    Name = "Elasticsearch Worker",
    Description = "Provides a background task to keep indices in sync with other instances.",
    Dependencies = ["Crest.Search.Elasticsearch"],
    Category = "Search"
)]

[assembly: Feature(
    Id = "Crest.Search.Elasticsearch.ContentPicker",
    Name = "Elasticsearch Content Picker",
    Description = "Provides a Elasticsearch content picker field editor.",
    Dependencies = ["Crest.Search.Elasticsearch", "Crest.ContentFields"],
    Category = "Search"
)]
