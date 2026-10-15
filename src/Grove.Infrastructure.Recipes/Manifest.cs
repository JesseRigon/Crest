using Grove.Modules.Manifest;

[assembly: Module(
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Infrastructure.Recipes",
    Name = "Recipes",
    Description = "The Recipes module allows you to execute recipe steps from json files.",
    Dependencies =
    [
        "Grove.Infrastructure.Recipes.Core",
        "Grove.Scripting",
    ],
    Category = "Infrastructure",
    IsAlwaysEnabled = true
)]

[assembly: Feature(
    Id = "Grove.Infrastructure.Recipes.Core",
    Name = "Recipes Core Services",
    Description = "Provides recipe core services.",
    Category = "Infrastructure",
    EnabledByDependencyOnly = true
)]
