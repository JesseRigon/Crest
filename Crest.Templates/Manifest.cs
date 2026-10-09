using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Templates",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.Templates",
    Name = "Templates",
    Description = "The Templates module provides a way to write custom shape templates from the admin.",
    Dependencies = ["Crest.Liquid"],
    Category = "Development"
)]

[assembly: Feature(
    Id = "Crest.AdminTemplates",
    Name = "Admin Templates",
    Description = "The Admin Templates module provides a way to write custom admin shape templates.",
    Dependencies = ["Crest.Templates"],
    Category = "Development"
)]
