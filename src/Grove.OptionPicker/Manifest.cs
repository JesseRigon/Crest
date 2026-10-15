using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Option Picker",
    Author = "Grove",
    Website = "https://grove.local",
    Version = "4.0.0.0.0",
    Description = "The option picker field, its option sources and their API.",
    Category = "Content"
)]

// Modules that bind fields to option sources (content part lists, parties, money, the ERP
// modules) declare this feature in their own dependencies, so enabling them enables it.
//   Grove.ContentFields.Indexing.SQL -> the text, numeric and boolean field index rows
//     ContentItemOptionSourceProvider joins for Field: path sort and filter pushdown.
//   Grove.Users -> the user option source reads the user index.
[assembly: Feature(
    Id = "Grove.OptionPicker",
    Name = "Option Picker",
    Description = "A content field that references options drawn from a source (content part lists, users, content items, cultures, time zones), with cascading filters and its API.",
    Category = "Content",
    Dependencies = ["Grove.Access", "Grove.ContentFields", "Grove.ContentFields.Indexing.SQL", "Grove.Contents", "Grove.ContentTypes", "Grove.Users"]
)]
