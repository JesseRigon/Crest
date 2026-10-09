using Crest.DisplayManagement.Manifest;
using Crest.Modules.Manifest;

[assembly: Theme(
    Name = "The Admin Theme",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "The default Admin theme.",
    Dependencies =
    [
        "Crest.Themes",
    ],
    Tags =
    [
        ManifestConstants.AdminTag,
    ]
)]
