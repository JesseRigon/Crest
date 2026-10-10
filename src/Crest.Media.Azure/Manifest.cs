using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Azure Media",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.Media.Azure.Storage",
    Name = "Azure Media Storage",
    Description = "Enables support for storing media files in Microsoft Azure Blob Storage.",
    Dependencies =
    [
        "Crest.Media.Cache"
    ],
    Category = "Hosting"
)]

[assembly: Feature(
    Id = "Crest.Media.Azure.ImageCache",
    Name = "Azure Media Image Cache",
    Description = "Enables support for storing cached resized images in Microsoft Azure Blob Storage.",
    Dependencies =
    [
        "Crest.Media"
    ],
    Category = "Hosting"
)]

[assembly: Feature(
    Id = "Crest.Media.Azure.ImageSharpImageCache",
    Name = "Azure Media Image Cache (Obsolete)",
    Description = "Obsolete legacy feature ID kept for backwards compatibility. Enables Crest.Media.Azure.ImageCache automatically.",
    Dependencies =
    [
        "Crest.Media.Azure.ImageCache"
    ],
    Category = "Hosting"
)]
