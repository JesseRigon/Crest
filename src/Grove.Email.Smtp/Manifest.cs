using Grove.Modules.Manifest;

[assembly: Module(
    Name = "SMTP Email Provider",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "Provides an email service provider leveraging Simple Mail Transfer Protocol (SMTP).",
    Dependencies =
    [
        "Grove.Email"
    ],
    Category = "Communication"
)]
