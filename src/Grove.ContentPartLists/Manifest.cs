using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Grove Content Part Lists",
    Author = "Grove",
    Website = "https://grove.local",
    Version = "4.0.0.0.0",
    Description = "Tenant-editable content part lists (enums) with shared global sets.",
    Category = "Grove"
)]

// Deliberately NOT IsAlwaysEnabled: a tenant opts into Content Part Lists. Consumers that
// require it (consuming modules) declare it in their own manifest Dependencies so
// enabling them enables this - features, not recipes, carry prerequisites.
[assembly: Feature(
    Id = "Grove.ContentPartLists",
    Name = "Grove Content Part Lists",
    Description = "Named, tenant-editable sets of options that content types can reference. Ships shared global sets (country codes, units of measure) every enabling tenant receives.",
    Category = "Grove",
    Dependencies = ["Grove", "Grove.Contents", "Grove.ContentTypes", "Grove.ContentFields", "Grove.Data.Global"]
)]
