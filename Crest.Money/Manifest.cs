using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Crest Money",
    Author = "OrchardCore.Crest",
    Version = "1.0.0",
    Category = "OrchardCore.Crest"
)]

[assembly: Feature(
    Id = "Crest.Money",
    Name = "Crest Money",
    Description = "The money content field and the currency table behind it: ISO 4217 metadata from the global store, a tenant-chosen default currency, and amounts that round to the right minor units everywhere.",
    Category = "OrchardCore.Crest",
    Dependencies = ["OrchardCore.Crest", "OrchardCore.ContentFields", "Crest.Global"]
)]
