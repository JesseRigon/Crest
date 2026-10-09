using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Setup",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "The setup module is creating the application's setup experience.",
    Dependencies = ["Crest.Recipes"],
    Category = "Infrastructure"
)]
