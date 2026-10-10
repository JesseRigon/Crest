using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Lucene",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.Lucene",
    Name = "Lucene",
    Description = "Creates Lucene indexes to support search scenarios, introduces a preconfigured container-enabled content type.",
    Dependencies =
    [
        "Crest.Queries.Core",
        "Crest.Indexing",
        "Crest.ContentTypes",
    ],
    Category = "Search"
)]

[assembly: Feature(
    Id = "Crest.Search.Lucene",
    Name = "Lucene (Obsolete)",
    Description = "Obsolete legacy feature ID kept for backwards compatibility. Enables Crest.Lucene automatically.",
    Dependencies = ["Crest.Lucene"],
    Category = "Search"
)]

[assembly: Feature(
    Id = "Crest.Search.Lucene.Worker",
    Name = "Lucene Worker",
    Description = "Provides a background task to keep local indices in sync with other instances.",
    Dependencies = ["Crest.Search.Lucene"],
    Category = "Search"
)]

[assembly: Feature(
    Id = "Crest.Search.Lucene.ContentPicker",
    Name = "Lucene Content Picker",
    Description = "Provides a Lucene content picker field editor.",
    Dependencies = ["Crest.Search.Lucene", "Crest.ContentFields"],
    Category = "Search"
)]
