using Grove.Modules.Manifest;
using Grove.Users;

[assembly: Module(
    Name = "Content Fields",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Grove.ContentFields",
    Name = "Content Fields",
    Category = "Content Management",
    Description = "Content Fields module adds common content fields to be used with your custom types.",
    Dependencies = ["Grove.ContentTypes", "Grove.Shortcodes"]
)]

[assembly: Feature(
    Id = "Grove.ContentFields.Indexing.SQL",
    Name = "Content Fields Indexing (SQL)",
    Category = "Content Management",
    Description = "Content Fields Indexing module adds database indexing for content fields.",
    Dependencies = ["Grove.ContentFields"]
)]

[assembly: Feature(
    Id = "Grove.ContentFields.Indexing.SQL.UserPicker",
    Name = "Content Fields Indexing (SQL) - User Picker",
    Category = "Content Management",
    Description = "User Picker Content Fields Indexing module adds database indexing for user picker fields.",
    Dependencies =
    [
        "Grove.ContentFields",
        UserConstants.Features.Users,
    ]
)]
