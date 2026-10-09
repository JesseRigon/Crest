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
