using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Liquid",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Grove.Display.Liquid",
    Name = "Liquid",
    Description = "The liquid module enables content items to have liquid syntax.",
    Dependencies = ["Grove.Display.Liquid.Core"],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Grove.Display.Liquid.Core",
    Name = "Liquid Core Services",
    Description = "Provides liquid core services.",
    EnabledByDependencyOnly = true,
    IsAlwaysEnabled = true,
    Category = "Content Management"
)]
