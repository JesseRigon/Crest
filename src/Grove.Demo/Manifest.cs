using Grove.Modules.Manifest;
using Grove.Users;

[assembly: Module(
    Name = "Grove Demo",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Name = "Grove Demo",
    Id = "Grove.Demo",
    Description = "Test",
    Category = "Samples",
    Dependencies =
    [
        UserConstants.Features.Users,
        "Grove.Contents",
    ]
)]

[assembly: Feature(
    Id = "Grove.Demo.Foo",
    Name = "Grove Foo Demo",
    Description = "Foo feature sample.",
    Category = "Samples"
)]
