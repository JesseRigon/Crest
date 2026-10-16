using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Hierarchy",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Contents.Hierarchy",
    Name = "Hierarchy",
    Description = "Introduces a preconfigured container-enabled content type.",
    Dependencies = ["Grove.Contents.Types"],
    Category = "Content Management"
)]
