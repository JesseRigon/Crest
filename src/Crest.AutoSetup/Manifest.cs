using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Auto Setup",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "The auto setup module allows to automatically install the application / tenants",
    Dependencies = ["Crest.Setup"],
    Category = "Infrastructure"
)]
