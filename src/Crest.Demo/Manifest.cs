using Crest.Modules.Manifest;
using Crest.Users;

[assembly: Module(
    Name = "Crest Demo",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Name = "Crest Demo",
    Id = "Crest.Demo",
    Description = "Test",
    Category = "Samples",
    Dependencies =
    [
        UserConstants.Features.Users,
        "Crest.Contents",
    ]
)]

[assembly: Feature(
    Id = "Crest.Demo.Foo",
    Name = "Crest Foo Demo",
    Description = "Foo feature sample.",
    Category = "Samples"
)]
