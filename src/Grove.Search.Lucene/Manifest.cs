using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Lucene",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Search.Lucene",
    Name = "Lucene",
    Description = "Creates Lucene indexes to support search scenarios, introduces a preconfigured container-enabled content type.",
    Dependencies =
    ["Grove.Access", 
        "Grove.Data.Queries.Core",
        "Grove.Data.Indexing",
        "Grove.Contents.Types",
    ],
    Category = "Search"
)]


[assembly: Feature(
    Id = "Grove.Search.Lucene.Worker",
    Name = "Lucene Worker",
    Description = "Provides a background task to keep local indices in sync with other instances.",
    Dependencies = ["Grove.Search.Lucene"],
    Category = "Search"
)]

