using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Liquid",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Crest.Liquid",
    Name = "Liquid",
    Description = "The liquid module enables content items to have liquid syntax.",
    Dependencies = ["Crest.Liquid.Core"],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Crest.Liquid.Core",
    Name = "Liquid Core Services",
    Description = "Provides liquid core services.",
    EnabledByDependencyOnly = true,
    IsAlwaysEnabled = true,
    Category = "Content Management"
)]
