using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Azure Media",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Media.Azure.Storage",
    Name = "Azure Media Storage",
    Description = "Stores file objects and the media library's files in Microsoft Azure Blob Storage.",
    Dependencies =
    [
        "Grove.Media.Cache"
    ],
    Category = "Hosting"
)]

[assembly: Feature(
    Id = "Grove.Media.Azure.ImageCache",
    Name = "Azure Media Image Cache",
    Description = "Enables support for storing cached resized images in Microsoft Azure Blob Storage.",
    Dependencies =
    [
        "Grove.Media"
    ],
    Category = "Hosting"
)]

[assembly: Feature(
    Id = "Grove.Media.Azure.ImageSharpImageCache",
    Name = "Azure Media Image Cache (Obsolete)",
    Description = "Obsolete legacy feature ID kept for backwards compatibility. Enables Grove.Media.Azure.ImageCache automatically.",
    Dependencies =
    [
        "Grove.Media.Azure.ImageCache"
    ],
    Category = "Hosting"
)]
