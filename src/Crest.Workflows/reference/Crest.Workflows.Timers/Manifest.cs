using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Crest Workflows Timers",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.Workflows.Timers",
    Name = "Timer Services and Activities",
    Description = "Provides common timer services and activities.",
    Category = "Crest.Workflows",
    Dependencies = ["Crest.Workflows"]
)]

[assembly: Feature(
    Id = "Crest.Workflows.Timers.Quartz",
    Name = "Quartz Timer Provider",
    Description = "Provides Quartz-based timer services. Suitable for clustered deployments.",
    Category = "Crest.Workflows",
    Dependencies = ["Crest.Workflows.Timers"]
)]