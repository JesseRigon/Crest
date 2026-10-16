using Grove.Modules.Manifest;

[assembly: Module(
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Name = "SMS",
    Id = "Grove.Communications.Sms",
    Description = "Provides settings and services to send SMS messages.",
    Category = "Communication",
    Dependencies =
    [
        "Grove.Communications.Screens",
    ]
)]
