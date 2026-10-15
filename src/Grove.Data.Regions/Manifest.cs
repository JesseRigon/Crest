using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Grove Regions",
    Author = "Grove",
    Version = "1.0.0",
    Category = "Grove"
)]

[assembly: Feature(
    Id = "Grove.Data.Regions",
    Name = "Grove Regions and Locations",
    Description = "Regional Profiles (a party's place, culture and currency), the geo tree every place resolves into, and per-country addressing maps.",
    Category = "Grove",
    Dependencies = ["Grove", "Grove.Contents", "Grove.Contents.Fields", "Grove.Contents.Title", "Grove.Data.Global"]
)]
