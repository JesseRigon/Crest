using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Amazon S3 Media",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.Media.AmazonS3",
    Name = "Amazon Media Storage",
    Description = "Enables support for storing media files in Amazon S3.",
    Dependencies =
    [
        "Crest.Media.Cache"
    ],
    Category = "Hosting"
)]

[assembly: Feature(
    Id = "Crest.Media.AmazonS3.ImageCache",
    Name = "Amazon Media Image Cache",
    Description = "Provides storage of cached resized images within the Amazon S3 storage service.",
    Dependencies =
    [
        "Crest.Media",
        "Crest.Media.AmazonS3"
    ],
    Category = "Hosting"
)]

[assembly: Feature(
    Id = "Crest.Media.AmazonS3.ImageSharpImageCache",
    Name = "Amazon Media Image Cache (Obsolete)",
    Description = "Obsolete legacy feature ID kept for backwards compatibility. Enables Crest.Media.AmazonS3.ImageCache automatically.",
    Dependencies =
    [
        "Crest.Media.AmazonS3.ImageCache"
    ],
    Category = "Hosting"
)]
