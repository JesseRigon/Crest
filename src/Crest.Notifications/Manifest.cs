using Crest.Modules.Manifest;

[assembly: Module(
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.Notifications",
    Name = "Notifications",
    Description = "Provides a way to notify users.",
    Category = "Notifications",
    Dependencies =
    [
        "Crest.Liquid"
    ]
)]

[assembly: Feature(
    Id = "Crest.Notifications.Email",
    Name = "Email Notifications",
    Description = "Provides a way to send email notifications to users.",
    Category = "Notifications",
    Dependencies =
    [
        "Crest.Notifications",
        "Crest.Email",
    ]
)]

[assembly: Feature(
    Id = "Crest.Notifications.Portal",
    Name = "Portal Notifications",
    Description = "Shows each user's notifications in Crest's admin and member shells: an indicator, the list, and marking as read.",
    Category = "Notifications",
    Dependencies =
    [
        "Crest.Notifications",
    ]
)]
