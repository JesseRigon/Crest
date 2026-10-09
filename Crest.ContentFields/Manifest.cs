using Crest.Modules.Manifest;
using Crest.Users;

[assembly: Module(
    Name = "Content Fields",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Crest.ContentFields",
    Name = "Content Fields",
    Category = "Content Management",
    Description = "Content Fields module adds common content fields to be used with your custom types.",
    Dependencies = ["Crest.ContentTypes", "Crest.Shortcodes"]
)]

[assembly: Feature(
    Id = "Crest.ContentFields.Indexing.SQL",
    Name = "Content Fields Indexing (SQL)",
    Category = "Content Management",
    Description = "Content Fields Indexing module adds database indexing for content fields.",
    Dependencies = ["Crest.ContentFields"]
)]

[assembly: Feature(
    Id = "Crest.ContentFields.Indexing.SQL.UserPicker",
    Name = "Content Fields Indexing (SQL) - User Picker",
    Category = "Content Management",
    Description = "User Picker Content Fields Indexing module adds database indexing for user picker fields.",
    Dependencies =
    [
        "Crest.ContentFields",
        UserConstants.Features.Users,
    ]
)]
