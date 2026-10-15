using Grove.DisplayManagement.Manifest;
using Grove.Modules.Manifest;

[assembly: Theme(
    Name = "The Admin Theme",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "The default Admin theme.",
    Dependencies =
    [
        "Grove.Themes",
    ],
    Tags =
    [
        ManifestConstants.AdminTag,
    ]
)]
