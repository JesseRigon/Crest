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
    Description = "Provides Azure AI Search services for managing indexes and facilitating search scenarios within indexes.",
    Dependencies =
    [
        "Grove.Data.Indexing",
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
