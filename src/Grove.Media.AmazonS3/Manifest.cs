using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Amazon S3 Media",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Media.AmazonS3",
    Name = "Amazon Media Storage",
    Description = "Stores file objects and the media library's files in Amazon S3.",
    Dependencies =
    [
        "Grove.Media.Cache"
    ],
    Category = "Hosting"
)]

[assembly: Feature(
    Id = "Grove.Media.AmazonS3.ImageCache",
    Name = "Amazon Media Image Cache",
    Description = "Provides storage of cached resized images within the Amazon S3 storage service.",
    Dependencies =
    [
        "Grove.Media",
        "Grove.Media.AmazonS3"
    ],
    Category = "Hosting"
)]

[assembly: Feature(
    Id = "Grove.Media.AmazonS3.ImageSharpImageCache",
    Name = "Amazon Media Image Cache (Obsolete)",
    Description = "Obsolete legacy feature ID kept for backwards compatibility. Enables Grove.Media.AmazonS3.ImageCache automatically.",
    Dependencies =
    [
        "Grove.Media.AmazonS3.ImageCache"
    ],
    Category = "Hosting"
)]
