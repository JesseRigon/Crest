using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Option Picker",
    Author = "Grove",
    Website = "https://grove.local",
    Version = "4.0.0.0.0",
    Description = "The option picker field, its option sources and their API.",
    Category = "Content"
)]

// Modules that bind fields to option sources (option lists, parties, money, the ERP
// modules) declare this feature in their own dependencies, so enabling them enables it.
//   Grove.Contents.Fields.Indexing.SQL -> the text, numeric and boolean field index rows
//     ContentItemOptionSourceProvider joins for Field: path sort and filter pushdown.
//   Grove.Access.Users -> the user option source reads the user index.
[assembly: Feature(
    Id = "Grove.Contents.OptionPicker",
    Name = "Option Picker",
    Description = "A content field that references options drawn from a source (option lists, users, content items, cultures, time zones), with cascading filters and its API.",
    Category = "Content",
    Dependencies = ["Grove.Access", "Grove.Contents.Fields", "Grove.Contents.Fields.Indexing.SQL", "Grove.Contents", "Grove.Contents.Types", "Grove.Access.Users"]
)]
