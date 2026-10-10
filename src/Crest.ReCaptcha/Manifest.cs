using Crest.Modules.Manifest;
using Crest.Users;

[assembly: Module(
    Name = "ReCaptcha",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion)]

[assembly: Feature(
    Id = "Crest.ReCaptcha",
    Name = "ReCaptcha",
    Category = "Security",
    Description = "Provides core ReCaptcha functionality.")]

[assembly: Feature(
    Id = "Crest.ReCaptcha.Users",
    Name = "ReCaptcha Users",
    Description = "Provides ReCaptcha functionality to harness login, register, forgot password and forms against robots.",
    Category = "Security",
    Dependencies =
    [
        "Crest.ReCaptcha",
        UserConstants.Features.Users,
    ])]
