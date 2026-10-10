using Crest.Modules.Manifest;

[assembly: Module(
    Name = "OpenApi",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.OpenApi",
    Name = "OpenApi",
    Description = "Microsoft.AspnetCore.OpenApi module for Crest.",
    Category = "Api"
)]

[assembly: Feature(
    Id = "Crest.OpenApi.SwaggerUI",
    Name = "OpenApi Swagger UI",
    Description = "Enables the Swagger UI interactive API explorer at ~/swagger.",
    Dependencies =
    [
        "Crest.OpenApi"
    ],
    Category = "Api"
)]

[assembly: Feature(
    Id = "Crest.OpenApi.ReDocUI",
    Name = "OpenApi ReDoc UI",
    Description = "Enables the ReDoc read-only API documentation at ~/redoc.",
    Dependencies =
    [
        "Crest.OpenApi"
    ],
    Category = "Api"
)]

[assembly: Feature(
    Id = "Crest.OpenApi.ScalarUI",
    Name = "OpenApi Scalar UI",
    Description = "Enables the Scalar modern API reference at ~/scalar/v1.",
    Dependencies =
    [
        "Crest.OpenApi"
    ],
    Category = "Api"
)]
