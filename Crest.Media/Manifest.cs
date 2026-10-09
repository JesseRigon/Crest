using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Media",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.Media",
    Name = "Media",
    Description = "The media module adds media management support.",
    Dependencies =
    [
        "Crest.ContentTypes"
    ],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Crest.Media.Indexing",
    Name = "Media Indexing",
    Description = "Provides a way to index media files with common format in search providers.",
    Dependencies =
    [
        "Crest.Media"
    ],
    Category = "Search",
    EnabledByDependencyOnly = true
)]

[assembly: Feature(
    Id = "Crest.Media.Indexing.Text",
    Name = "Text Media Indexing",
    Description = "Provides a way to index common text files like (.txt and .md) in search providers.",
    Dependencies =
    [
        "Crest.Media.Indexing"
    ],
    Category = "Search"
)]

[assembly: Feature(
    Id = "Crest.Media.Cache",
    Name = "Media Cache",
    Description = "The media cache module adds remote file store cache support.",
    Dependencies =
    [
        "Crest.Media"
    ],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Crest.Media.Slugify",
    Name = "Media Slugify",
    Description = "The media slugify module transforms newly created folders and files into SEO-friendly versions by generating slugs.",
    Dependencies =
    [
        "Crest.Media"
    ],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Crest.Media.Security",
    Name = "Secure Media",
    Description = "Adds permissions to restrict access to media folders.",
    Dependencies =
    [
        "Crest.Media"
    ],
    After =
    [
        "Crest.Users"
    ],
    Before =
    [
        "Crest.Media"
    ],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Crest.Media.Tus",
    Name = "Media TUS Uploads",
    Description = "Enables resumable file uploads using the TUS protocol. When enabled, replaces the default chunked upload mechanism with the TUS standard, allowing uploads to be paused and resumed.",
    Dependencies =
    [
        "Crest.Media"
    ],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Crest.Media.SignalR",
    Name = "Media SignalR",
    Description = "Enables real-time media updates via SignalR. When enabled, changes to media files and folders are broadcast to connected clients.",
    Dependencies =
    [
        "Crest.Media",
        "Crest.SignalR"
    ],
    Category = "Content Management"
)]
