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
        "Grove.Infrastructure.Settings",
        "Grove.Display.Liquid"
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
        "Grove.Infrastructure.Recipes.Core",
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

// Content Part Lists ship with Contents but stay opt-in: consumers (parties, money, members, the
// ERP modules) declare this feature in their own dependencies, so enabling them enables it.
[assembly: Feature(
    Id = "Grove.Contents.PartLists",
    Name = "Content Part Lists",
    Description = "Named, tenant-editable sets of options that content types can reference. Ships shared global sets (country codes, units of measure) every enabling tenant receives.",
    Category = "Content Management",
    Dependencies = ["Grove", "Grove.Contents", "Grove.Contents.Types", "Grove.Contents.Fields", "Grove.Data.Global", "Grove.Contents.OptionPicker"]
)]
