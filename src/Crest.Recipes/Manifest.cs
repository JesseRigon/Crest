using Crest.Modules.Manifest;

[assembly: Module(
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.Recipes",
    Name = "Recipes",
    Description = "The Recipes module allows you to execute recipe steps from json files.",
    Dependencies =
    [
        "Crest.Recipes.Core",
        "Crest.Scripting",
    ],
    Category = "Infrastructure",
    IsAlwaysEnabled = true
)]

[assembly: Feature(
    Id = "Crest.Recipes.Core",
    Name = "Recipes Core Services",
    Description = "Provides recipe core services.",
    Category = "Infrastructure",
    EnabledByDependencyOnly = true
)]
