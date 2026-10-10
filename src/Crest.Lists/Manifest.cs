using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Lists",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.Lists",
    Name = "Lists",
    Description = "Introduces a preconfigured container-enabled content type.",
    Dependencies = ["Crest.ContentTypes"],
    Category = "Content Management"
)]
