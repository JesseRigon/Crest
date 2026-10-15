using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Azure Communication Services Email",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "Provides email service providers leveraging Azure Communication Services (ACS).",
    Dependencies =
    [
        "Grove.Email",
    ],
    Category = "Communication"
)]
