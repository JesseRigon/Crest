using Grove.Modules.Manifest;

[assembly: Module(
    Name = "OpenApi",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.OpenApi",
    Name = "OpenApi",
    Description = "Microsoft.AspnetCore.OpenApi module for Grove.",
    Category = "Api"
)]

[assembly: Feature(
    Id = "Grove.OpenApi.SwaggerUI",
    Name = "OpenApi Swagger UI",
    Description = "Enables the Swagger UI interactive API explorer at ~/swagger.",
    Dependencies =
    [
        "Grove.OpenApi"
    ],
    Category = "Api"
)]

[assembly: Feature(
    Id = "Grove.OpenApi.ReDocUI",
    Name = "OpenApi ReDoc UI",
    Description = "Enables the ReDoc read-only API documentation at ~/redoc.",
    Dependencies =
    [
        "Grove.OpenApi"
    ],
    Category = "Api"
)]

[assembly: Feature(
    Id = "Grove.OpenApi.ScalarUI",
    Name = "OpenApi Scalar UI",
    Description = "Enables the Scalar modern API reference at ~/scalar/v1.",
    Dependencies =
    [
        "Grove.OpenApi"
    ],
    Category = "Api"
)]
