using Crest.Modules.Manifest;

[assembly: Module(
    Name = "SignalR Redis Backplane",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "Routes SignalR messages across application nodes through a tenant-qualified Redis backplane.",
    Category = "Infrastructure"
)]

[assembly: Feature(
    Id = "Crest.SignalR.Redis",
    Name = "SignalR Redis Backplane",
    Description = "Uses Redis as the SignalR backplane, enabling multi-instance deployments with a tenant-qualified channel prefix.",
    Category = "Infrastructure",
    Dependencies =
    [
        "Crest.SignalR",
        "Crest.Redis",
    ]
)]
