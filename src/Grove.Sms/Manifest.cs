using Grove.Modules.Manifest;

[assembly: Module(
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Name = "SMS",
    Id = "Grove.Sms",
    Description = "Provides settings and services to send SMS messages.",
    Category = "Communication"
)]

[assembly: Feature(
    Name = "SMS Notifications",
    Id = "Grove.Notifications.Sms",
    Description = "Provides a way to send SMS notifications to users.",
    Category = "Notifications",
    Dependencies =
    [
        "Grove.Notifications",
        "Grove.Sms",
    ]
)]
