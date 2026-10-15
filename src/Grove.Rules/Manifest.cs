using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Rules",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Rules",
    Name = "Rules",
    Description = "The Rules module adds rule building capabilities.",
    Dependencies =
    ["Grove.Access", 
        "Grove.Scripting"
    ],
    Category = "Infrastructure"
)]
