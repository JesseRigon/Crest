using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Crest Money",
    Author = "Crest",
    Version = "1.0.0",
    Category = "Crest"
)]

[assembly: Feature(
    Id = "Crest.Money",
    Name = "Crest Money",
    Description = "The money content field and the currency table behind it: ISO 4217 metadata from the global store, a tenant-chosen default currency, and amounts that round to the right minor units everywhere.",
    Category = "Crest",
    Dependencies = ["Crest", "Crest.ContentFields", "Crest.Global"]
)]
