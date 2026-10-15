using Grove.Modules.Manifest;
using Grove.Users;

[assembly: Module(
    Name = "ReCaptcha",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion)]

[assembly: Feature(
    Id = "Grove.ReCaptcha",
    Name = "ReCaptcha",
    Category = "Security",
    Description = "Provides core ReCaptcha functionality.")]

[assembly: Feature(
    Id = "Grove.ReCaptcha.Users",
    Name = "ReCaptcha Users",
    Description = "Provides ReCaptcha functionality to harness login, register, forgot password and forms against robots.",
    Category = "Security",
    Dependencies =
    [
        "Grove.ReCaptcha",
        UserConstants.Features.Users,
    ])]
