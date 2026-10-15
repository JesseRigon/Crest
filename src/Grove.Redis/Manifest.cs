using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Redis",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Redis",
    Name = "Redis",
    Description = "Redis configuration support.",
    Category = "Distributed"
)]

[assembly: Feature(
    Id = "Grove.Redis.Cache",
    Name = "Redis Cache",
    Description = "Distributed cache using Redis.",
    Dependencies = ["Grove.Redis"],
    Category = "Distributed"
)]

[assembly: Feature(
    Id = "Grove.Redis.Bus",
    Name = "Redis Bus",
    Description = "Makes the Signal service distributed.",
    Dependencies = ["Grove.Redis"],
    Category = "Distributed"
)]

[assembly: Feature(
    Id = "Grove.Redis.Lock",
    Name = "Redis Lock",
    Description = "Distributed Lock using Redis.",
    Dependencies = ["Grove.Redis"],
    Category = "Distributed"
)]

[assembly: Feature(
    Id = "Grove.Redis.DataProtection",
    Name = "Distributed Data Protection (Redis)",
    Description = "Enables distributed data protection using Redis; recommended only with a Redis server configured for persistence.",
    Dependencies = ["Grove.Redis"],
    Category = "Security"
)]
