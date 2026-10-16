using Grove.Modules.Manifest;

[assembly: Module(
    Name = "IMAP",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "The IMAP protocol of the connector engine, and an e-mail receive provider that reads a mailbox through an IMAP connection.",
    Dependencies =
    [
        "Grove.Communications.Email",
        "Grove.Data.Connectors",
    ],
    Category = "Communication"
)]
