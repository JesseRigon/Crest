using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Setup",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "The setup module is creating the application's setup experience.",
    Dependencies = ["Grove.Infrastructure.Recipes"],
    Category = "Infrastructure"
)]
