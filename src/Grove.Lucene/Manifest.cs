using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Lucene",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Lucene",
    Name = "Lucene",
    Description = "Creates Lucene indexes to support search scenarios, introduces a preconfigured container-enabled content type.",
    Dependencies =
    ["Grove.Access", 
        "Grove.Queries.Core",
        "Grove.Indexing",
        "Grove.ContentTypes",
    ],
    Category = "Search"
)]

[assembly: Feature(
    Id = "Grove.Search.Lucene",
    Name = "Lucene (Obsolete)",
    Description = "Obsolete legacy feature ID kept for backwards compatibility. Enables Grove.Lucene automatically.",
    Dependencies = ["Grove.Lucene"],
    Category = "Search"
)]

[assembly: Feature(
    Id = "Grove.Search.Lucene.Worker",
    Name = "Lucene Worker",
    Description = "Provides a background task to keep local indices in sync with other instances.",
    Dependencies = ["Grove.Search.Lucene"],
    Category = "Search"
)]

[assembly: Feature(
    Id = "Grove.Search.Lucene.ContentPicker",
    Name = "Lucene Content Picker",
    Description = "Provides a Lucene content picker field editor.",
    Dependencies = ["Grove.Search.Lucene", "Grove.ContentFields"],
    Category = "Search"
)]
