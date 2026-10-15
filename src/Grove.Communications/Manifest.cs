using Grove.Modules.Manifest;

[assembly: Module(
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Communications",
    Name = "Notifications",
    Description = "Provides a way to notify users.",
    Category = "Notifications",
    Dependencies =
    [
        "Grove.Display.Liquid"
    ]
)]

[assembly: Feature(
    Id = "Grove.Communications.Notifications.Email",
    Name = "Email Notifications",
    Description = "Provides a way to send email notifications to users.",
    Category = "Notifications",
    Dependencies =
    [
        "Grove.Communications",
        "Grove.Communications.Email",
    ]
)]

[assembly: Feature(
    Id = "Grove.Communications.Notifications.Portal",
    Name = "Portal Notifications",
    Description = "Shows each user's notifications in Grove's admin and member shells: an indicator, the list, and marking as read.",
    Category = "Notifications",
    Dependencies =
    [
        "Grove.Communications",
    ]
)]
