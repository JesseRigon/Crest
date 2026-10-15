using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Grove Money",
    Author = "Grove",
    Version = "1.0.0",
    Category = "Grove"
)]

[assembly: Feature(
    Id = "Grove.Data.Types.Money",
    Name = "Grove Money",
    Description = "The money content field and the currency table behind it: ISO 4217 metadata from the global store, a tenant-chosen default currency, and amounts that round to the right minor units everywhere.",
    Category = "Grove",
    Dependencies = ["Grove", "Grove.Contents.Fields", "Grove.Data.Global", "Grove.Contents.OptionPicker"]
)]
