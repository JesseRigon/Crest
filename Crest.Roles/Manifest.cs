using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Roles",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Category = "Security"
)]

[assembly: Feature(
    Id = "Crest.Roles",
    Name = "Roles",
    Description = "Provides permissions to assign roles to users. Additionally, it updates default roles with default permissions provided by features.",
    Dependencies = ["Crest.Roles.Core"],
    Category = "Security"
)]

[assembly: Feature(
    Id = "Crest.Roles.Core",
    Name = "Roles Core Services",
    Description = "Provides role core services.",
    EnabledByDependencyOnly = true,
    Category = "Security"
)]
