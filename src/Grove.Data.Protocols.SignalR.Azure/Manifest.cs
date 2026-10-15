using Grove.Modules.Manifest;

[assembly: Module(
    Name = "SignalR Azure Backplane",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "Routes SignalR messages across application nodes through the Azure SignalR Service.",
    Category = "Infrastructure"
)]

[assembly: Feature(
    Id = "Grove.Data.Protocols.SignalR.Azure",
    Name = "SignalR Azure Backplane",
    Description = "Uses the Azure SignalR Service as the SignalR backplane, enabling multi-instance deployments.",
    Category = "Infrastructure",
    Dependencies =
    [
        "Grove.Data.Protocols.SignalR",
    ]
)]
