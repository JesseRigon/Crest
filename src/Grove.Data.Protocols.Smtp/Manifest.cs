using Grove.Modules.Manifest;

[assembly: Module(
    Name = "SMTP",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "The SMTP protocol of the connector engine, and an e-mail provider that sends through an SMTP connection.",
    Dependencies =
    [
        "Grove.Communications.Email",
        "Grove.Data.Connectors",
    ],
    Category = "Communication"
)]
