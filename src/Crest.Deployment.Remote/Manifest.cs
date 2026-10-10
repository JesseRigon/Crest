using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Remote Deployment",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "Provide the ability to export and import to and from a remote server.",
    Dependencies = ["Crest.Deployment"],
    Category = "Deployment"
)]
