using Grove.Modules.Manifest;

[assembly: Module(
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Notifications",
    Name = "Notifications",
    Description = "Provides a way to notify users.",
    Category = "Notifications",
    Dependencies =
    [
        "Grove.Liquid"
    ]
)]

[assembly: Feature(
    Id = "Grove.Notifications.Email",
    Name = "Email Notifications",
    Description = "Provides a way to send email notifications to users.",
    Category = "Notifications",
    Dependencies =
    [
        "Grove.Notifications",
        "Grove.Email",
    ]
)]

[assembly: Feature(
    Id = "Grove.Notifications.Portal",
    Name = "Portal Notifications",
    Description = "Shows each user's notifications in Grove's admin and member shells: an indicator, the list, and marking as read.",
    Category = "Notifications",
    Dependencies =
    [
        "Grove.Notifications",
    ]
)]
