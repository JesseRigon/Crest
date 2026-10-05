using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Crest Regions",
    Author = "Crest",
    Version = "1.0.0",
    Category = "Crest"
)]

[assembly: Feature(
    Id = "Crest.Regions",
    Name = "Crest Regions and Locations",
    Description = "Localization Profiles (a party's place, culture and currency), the geo tree every place resolves into, and per-country addressing maps.",
    Category = "Crest",
    Dependencies = ["Crest", "OrchardCore.Contents", "OrchardCore.ContentFields", "OrchardCore.Title", "Crest.Global"]
)]
