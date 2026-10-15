using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Lists",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Lists",
    Name = "Lists",
    Description = "Introduces a preconfigured container-enabled content type.",
    Dependencies = ["Grove.ContentTypes"],
    Category = "Content Management"
)]
