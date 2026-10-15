using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Media",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Media",
    Name = "Media",
    Description = "The media module adds media management support.",
    Dependencies =
    ["Grove.Access",
        "Grove.Contents.Types",
        // Every upload is scanned (docs/media.md, requirement 86): no Media without scanning.
        "Grove.Media.Scanning",
    ],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Grove.Media.Indexing",
    Name = "Media Indexing",
    Description = "Provides a way to index media files with common format in search providers.",
    Dependencies =
    [
        "Grove.Media"
    ],
    Category = "Search",
    EnabledByDependencyOnly = true
)]

[assembly: Feature(
    Id = "Grove.Media.Indexing.Text",
    Name = "Text Media Indexing",
    Description = "Provides a way to index common text files like (.txt and .md) in search providers.",
    Dependencies =
    [
        "Grove.Media.Indexing"
    ],
    Category = "Search"
)]

[assembly: Feature(
    Id = "Grove.Media.Cache",
    Name = "Media Cache",
    Description = "The media cache module adds remote file store cache support.",
    Dependencies =
    [
        "Grove.Media"
    ],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Grove.Media.Slugify",
    Name = "Media Slugify",
    Description = "The media slugify module transforms newly created folders and files into SEO-friendly versions by generating slugs.",
    Dependencies =
    [
        "Grove.Media"
    ],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Grove.Media.Security",
    Name = "Secure Media",
    Description = "Adds permissions to restrict access to media folders.",
    Dependencies =
    [
        "Grove.Media"
    ],
    After =
    [
        "Grove.Access.Users"
    ],
    Before =
    [
        "Grove.Media"
    ],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Grove.Media.Tus",
    Name = "Media TUS Uploads",
    Description = "Enables resumable file uploads using the TUS protocol. When enabled, replaces the default chunked upload mechanism with the TUS standard, allowing uploads to be paused and resumed.",
    Dependencies =
    [
        "Grove.Media"
    ],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Grove.Media.SignalR",
    Name = "Media SignalR",
    Description = "Enables real-time media updates via SignalR. When enabled, changes to media files and folders are broadcast to connected clients.",
    Dependencies =
    [
        "Grove.Media",
        "Grove.Data.Protocols.SignalR"
    ],
    Category = "Content Management"
)]
