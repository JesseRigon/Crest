using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Azure AI Search",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.AzureAI",
    Name = "Azure AI Search",
    Description = "Provides Azure AI Search services for managing indexes and facilitating search scenarios within indexes.",
    Dependencies =
    [
        "Crest.Indexing",
    ],
    Category = "Search"
)]

[assembly: Feature(
    Id = "Crest.Search.AzureAI",
    Name = "Azure AI Search (Obsolete)",
    Description = "Obsolete legacy feature ID kept for backwards compatibility. Enables Crest.AzureAI automatically.",
    Dependencies =
    [
        "Crest.AzureAI",
    ],
    Category = "Search"
)]
