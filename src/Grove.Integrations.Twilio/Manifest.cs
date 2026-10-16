using Grove.Integrations.Twilio;
using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Twilio",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Category = "Communication"
)]

[assembly: Feature(
    Id = TwilioConnection.FeatureId,
    Name = "Twilio SMS",
    Category = "Communication",
    Description = "Declares the Twilio connection and sends SMS messages through it.",
    Dependencies =
    [
        "Grove.Communications.Sms",
        "Grove.Data.Connectors",
    ]
)]
