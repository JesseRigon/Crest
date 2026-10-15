using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Contents",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Contents",
    Name = "Contents",
    Description = "The contents module enables the edition and rendering of content items.",
    Dependencies =
    ["Grove.Access", 
        "Grove.Settings",
        "Grove.Liquid"
    ],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Grove.Contents.VersionPruning",
    Name = "Content Version Pruning",
    Description = "Provides a background task to prune old content item versions.",
    Dependencies = ["Grove.Contents"],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Grove.Contents.FileContentDefinition",
    Name = "File Content Definition",
    Description = "Stores Content Definition in a local file.",
    Dependencies = ["Grove.Contents"],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Grove.Contents.Deployment.ExportContentToDeploymentTarget",
    Name = "Export Content To Deployment Target",
    Description = "Adds an export to deployment target action to the content items list.",
    Dependencies =
    [
        "Grove.Contents",
        "Grove.Deployment",
        "Grove.Recipes.Core",
    ],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Grove.Contents.Deployment.AddToDeploymentPlan",
    Name = "Add Content To Deployment Plan",
    Description = "Adds an add to deployment plan action to the content items list.",
    Dependencies = ["Grove.Contents", "Grove.Deployment"],
    Category = "Content Management"
)]

[assembly: Feature(
    Id = "Grove.Contents.Deployment.Download",
    Name = "View Or Download Content As JSON",
    Description = "View or download content as JSON from the content items list.",
    Dependencies = ["Grove.Contents", "Grove.Deployment"],
    Category = "Content Management"
)]
