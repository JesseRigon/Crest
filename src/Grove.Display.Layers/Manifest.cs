using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Layers",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Display.Layers",
    Name = "Layers",
    Description = "Places components into zones across the site's pages, by rules.",
    Dependencies =
    ["Grove.Access",
        "Grove.Scripting",
        "Grove.Display.Rules"
    ],
    Category = "Content"
)]
