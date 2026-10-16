using Grove.Modules.Manifest;
using Grove.Access.Users;

[assembly: Module(
    Name = "Content Fields",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Grove.Contents.Fields",
    Name = "Content Fields",
    Category = "Content Management",
    Description = "Content Fields module adds common content fields to be used with your custom types.",
    Dependencies = ["Grove.Contents.Types", "Grove.Shortcodes"]
)]

[assembly: Feature(
    Id = "Grove.Contents.Fields.Indexing.SQL",
    Name = "Content Fields Indexing (SQL)",
    Category = "Content Management",
    Description = "Content Fields Indexing module adds database indexing for content fields.",
    Dependencies = ["Grove.Contents.Fields"]
)]

