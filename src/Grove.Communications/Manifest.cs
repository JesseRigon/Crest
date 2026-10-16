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
    Id = "Grove.Communications.Notifications.Sms",
    Name = "SMS Notifications",
    Description = "Provides a way to send SMS notifications to users.",
    Category = "Notifications",
    Dependencies =
    [
        "Grove.Communications",
        "Grove.Communications.Sms",
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

[assembly: Feature(
    Id = "Grove.Communications.Screens",
    Name = "Communications",
    Description = "The communications screens: each channel's providers, connections and test sends in one place, and the delivery log of every send.",
    Category = "Communication"
)]
