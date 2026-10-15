using Grove.Modules.Manifest;

[assembly: Module(
    Name = "SignalR",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "Provides the services required to host and consume SignalR hubs.",
    Category = "Infrastructure"
)]

[assembly: Feature(
    Id = "Grove.SignalR",
    Name = "SignalR",
    Description = "Registers SignalR and the SignalR client resources.",
    Category = "Infrastructure"
)]
