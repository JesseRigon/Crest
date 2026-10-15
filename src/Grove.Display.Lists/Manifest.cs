using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Lists",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Display.Lists",
    Name = "Lists",
    Description = "Introduces a preconfigured container-enabled content type.",
    Dependencies = ["Grove.Contents.Types"],
    Category = "Content Management"
)]
