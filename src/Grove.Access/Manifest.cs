using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Access",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "The one access machinery every read and write goes through: the caller built per request, the decision, the scope and the audit.",
    Category = "Security",
    Dependencies =
    [
        "Grove.Roles",
        "Grove.Users",
        "Grove.Settings",
    ],
    IsAlwaysEnabled = true
)]
