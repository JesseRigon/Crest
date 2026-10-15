using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Redis",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Integrations.Redis",
    Name = "Redis",
    Description = "Redis configuration support.",
    Category = "Distributed"
)]

[assembly: Feature(
    Id = "Grove.Integrations.Redis.Cache",
    Name = "Redis Cache",
    Description = "Distributed cache using Redis.",
    Dependencies = ["Grove.Integrations.Redis"],
    Category = "Distributed"
)]

[assembly: Feature(
    Id = "Grove.Integrations.Redis.Bus",
    Name = "Redis Bus",
    Description = "Makes the Signal service distributed.",
    Dependencies = ["Grove.Integrations.Redis"],
    Category = "Distributed"
)]

[assembly: Feature(
    Id = "Grove.Integrations.Redis.Lock",
    Name = "Redis Lock",
    Description = "Distributed Lock using Redis.",
    Dependencies = ["Grove.Integrations.Redis"],
    Category = "Distributed"
)]

[assembly: Feature(
    Id = "Grove.Integrations.Redis.DataProtection",
    Name = "Distributed Data Protection (Redis)",
    Description = "Enables distributed data protection using Redis; recommended only with a Redis server configured for persistence.",
    Dependencies = ["Grove.Integrations.Redis"],
    Category = "Security"
)]
