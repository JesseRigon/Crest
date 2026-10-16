using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Azure AI Search",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Integrations.AI.Azure",
    Name = "Azure AI Search",
    Description = "Azure AI Search indexes and search, on a connection to the search service (Connections).",
    Dependencies =
    [
        "Grove.Data.Indexing",
        "Grove.Data.Connectors",
    ],
    Category = "Search"
)]

[assembly: Feature(
    Id = "Grove.Search.AzureAI",
    Name = "Azure AI Search (Obsolete)",
    Description = "Obsolete legacy feature ID kept for backwards compatibility. Enables Grove.Integrations.AI.Azure automatically.",
    Dependencies =
    [
        "Grove.Integrations.AI.Azure",
    ],
    Category = "Search"
)]
