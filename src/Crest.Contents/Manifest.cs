using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Contents",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.Contents",
    Name = "Contents",
    Description = "The contents module enables the edition and rendering of content items.",
    Dependencies =
    [
        "Crest.Settings",
        "Crest.Liquid"
    ],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Crest.Contents.VersionPruning",
    Name = "Content Version Pruning",
    Description = "Provides a background task to prune old content item versions.",
    Dependencies = ["Crest.Contents"],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Crest.Contents.FileContentDefinition",
    Name = "File Content Definition",
    Description = "Stores Content Definition in a local file.",
    Dependencies = ["Crest.Contents"],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Crest.Contents.Deployment.ExportContentToDeploymentTarget",
    Name = "Export Content To Deployment Target",
    Description = "Adds an export to deployment target action to the content items list.",
    Dependencies =
    [
        "Crest.Contents",
        "Crest.Deployment",
        "Crest.Recipes.Core",
    ],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Crest.Contents.Deployment.AddToDeploymentPlan",
    Name = "Add Content To Deployment Plan",
    Description = "Adds an add to deployment plan action to the content items list.",
    Dependencies = ["Crest.Contents", "Crest.Deployment"],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Crest.Contents.Deployment.Download",
    Name = "View Or Download Content As JSON",
    Description = "View or download content as JSON from the content items list.",
    Dependencies = ["Crest.Contents", "Crest.Deployment"],
    Category = "Content Management"
)]
