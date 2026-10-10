using Crest.Modules.Manifest;

[assembly: Module(
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Name = "Azure Communication Services SMS",
    Id = "Crest.Sms.Azure",
    Description = "Enables the ability to send SMS messages through Azure Communication Services (ACS).",
    Dependencies =
    [
        "Crest.Sms",
    ],
    Category = "Communication"
)]
