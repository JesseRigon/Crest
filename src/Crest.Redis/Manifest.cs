using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Redis",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.Redis",
    Name = "Redis",
    Description = "Redis configuration support.",
    Category = "Distributed"
)]

[assembly: Feature(
    Id = "Crest.Redis.Cache",
    Name = "Redis Cache",
    Description = "Distributed cache using Redis.",
    Dependencies = ["Crest.Redis"],
    Category = "Distributed"
)]

[assembly: Feature(
    Id = "Crest.Redis.Bus",
    Name = "Redis Bus",
    Description = "Makes the Signal service distributed.",
    Dependencies = ["Crest.Redis"],
    Category = "Distributed"
)]

[assembly: Feature(
    Id = "Crest.Redis.Lock",
    Name = "Redis Lock",
    Description = "Distributed Lock using Redis.",
    Dependencies = ["Crest.Redis"],
    Category = "Distributed"
)]

[assembly: Feature(
    Id = "Crest.Redis.DataProtection",
    Name = "Distributed Data Protection (Redis)",
    Description = "Enables distributed data protection using Redis; recommended only with a Redis server configured for persistence.",
    Dependencies = ["Crest.Redis"],
    Category = "Security"
)]
