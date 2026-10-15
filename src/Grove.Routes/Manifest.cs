using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Routes",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "The tenant's route management: the page registry's routes, content routes and the URL rewrite and redirect rules.",
    Category = "Infrastructure"
)]

[assembly: Feature(
    Id = "Grove.Routes",
    Name = "Routes",
    Description = "Lists the tenant's routes (pages, content routes, the home route) and applies the URL rewrite and redirect rules to incoming requests.",
    Category = "Infrastructure"
)]
