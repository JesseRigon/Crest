using OrchardCore.DisplayManagement.Manifest;

[assembly: Theme(
    Id = "OrchardCore.Crest.Member",
    Name = "Orchard Crest UI Framework Member",
    BaseTheme = "",
    Author = "Orchard Crest UI Framework",
    Website = "https://github.com/OrchardCore.Crest/Orchard-Crest",
    Version = "3.0.0.0.0",
    Description = "A Blazor WebAssembly member portal theme for Orchard Core using Crest components.",
    // "member" is the bucket tag, the member-shell counterpart of the admin theme's
    // "admin" tag: it is how a theme declares which shell it hosts, so a fork of this
    // theme is recognized as a member theme without its id being hardcoded anywhere.
    // "blazor" marks it as hosting a Crest Blazor shell document.
    Tags = ["member", "blazor", "radzen"]
)]
