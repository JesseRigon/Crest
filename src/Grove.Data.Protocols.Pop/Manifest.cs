using Grove.Modules.Manifest;

[assembly: Module(
    Name = "POP3",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "The POP3 protocol of the connector engine, and an e-mail receive provider that reads a mailbox through a POP3 connection.",
    Dependencies =
    [
        "Grove.Communications.Email",
        "Grove.Data.Connectors",
    ],
    Category = "Communication"
)]
