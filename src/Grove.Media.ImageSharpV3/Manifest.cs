using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Media ImageSharp",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Media.ImageSharpV3",
    Name = "Media ImageSharp Image Processing",
    Description = "Replaces the default media image processing engine with an ImageSharp (v3) based implementation.",
    Dependencies =
    [
        "Grove.Media"
    ],
    Category = "Content Management"
)]
