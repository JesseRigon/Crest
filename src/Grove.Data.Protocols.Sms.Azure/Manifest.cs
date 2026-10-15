using Grove.Modules.Manifest;

[assembly: Module(
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Name = "Azure Communication Services SMS",
    Id = "Grove.Data.Protocols.Sms.Azure",
    Description = "Enables the ability to send SMS messages through Azure Communication Services (ACS).",
    Dependencies =
    [
        "Grove.Communications.Sms",
    ],
    Category = "Communication"
)]
