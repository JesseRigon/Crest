using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Rate Limits",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.RateLimits",
    Name = "Rate Limits",
    Description = "Provides a way to manage rate limiting to the website.",
    Category = "Security",
    Priority = "-100", // Ensure that the Rate Limits feature is loaded before other features that may depend on it.
    After =
    [
        "Crest.Tenants.FileProvider"
    ],
    Before = 
    [
        "Crest.Api.GraphQL",
        "Crest.Seo"
    ]
)]
